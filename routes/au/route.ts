import User from '@/database/User';
import Database from '@/database/Database';
import { Router, Request, Response } from 'express';
import {
    normalizeIp,
    hashIp,
    generateUserKey,
    USER_KEY_CHUNKS,
} from '@/utils';

export const apiUserRouter = Router();

// /aur : enregistrement — crée le compte et renvoie la clé
apiUserRouter.get('/aur', registerHandler);
// /aul{3hex} : login par chunks (Udon ne peut pas construire d'URL dynamiques)
apiUserRouter.get('/aul:chunk', loginChunkHandler);

function userPayload(user: User, extra: Record<string, unknown> = {}) {
    return {
        link_type: 'u',
        link_id: '',
        iid: null,
        uid: user.id,
        time: Date.now(),
        data: {
            last_seen_at: user.last_seen_at.getTime()
        },
        ...extra,
    };
}

/**
 * GET /aur — premier lancement (ou nouveau login)
 * Réponse : { link_type:'u', uid, key, data:{ last_seen_at } }
 */
async function registerHandler(req: Request, res: Response) {
    try {
        // On ne stocke JAMAIS l'IP en clair : uniquement son hash SHA-256
        const ipHash = hashIp(normalizeIp(req));

        // Nouveau compte : l'id EST la clé (12 hex)
        let user: User | null = null;
        for (let attempt = 0; attempt < 5; attempt++) {
            try {
                user = await User.createWithKey(generateUserKey(), ipHash);
                break;
            } catch (error: any) {
                if (error?.code !== 'P2002') throw error; // collision de clé improbable → retry
            }
        }
        if (!user) {
            return res.status(500).json({ error: 'Unable to allocate user key' });
        }

        user = await user.updateLastSeen();

        return res.json(userPayload(user, { key: user.id }));
    } catch (error) {
        console.error('Error in /aur:', error);
        return res.status(500).json({ error: 'Registration failed' });
    }
}

// =============================================================================
// Login par chunks
// La clé de 12 hex est transmise en 4 chunks de 3 hex via des URLs statiques
// (/aul{3hex}). Les chunks arrivent sans information d'ordre (Udon), donc on
// les bufferise par IP (TTL 90s) puis on reconstitue la clé par permutation.
// =============================================================================

const LOGIN_BUFFER_TTL_MS = 90_000;

interface LoginBufferEntry {
    chunks: Set<string>;
    lastSeen: number;
}

const loginBuffers = new Map<string, LoginBufferEntry>();

function permutations(values: string[]): string[][] {
    if (values.length <= 1) return [values];
    const out: string[][] = [];
    for (let i = 0; i < values.length; i++) {
        const rest = [...values.slice(0, i), ...values.slice(i + 1)];
        for (const p of permutations(rest)) {
            out.push([values[i], ...p]);
        }
    }
    return out;
}

/** Reconstruit la clé à partir des 4 chunks (24 permutations possibles) */
async function findUserByChunks(chunks: string[]): Promise<User | null> {
    const candidates = permutations(chunks).map(p => p.join(''));
    const users = await Database.prisma.user.findMany({
        where: { id: { in: candidates } },
    });

    // Exactement 1 candidat connu → clé valide (0 = inconnue, >1 = ambiguë)
    if (users.length !== 1) return null;
    return new User(users[0]);
}

/**
 * GET /aul{3hex} — un chunk de la clé de login
 * GET /aul{12hex} — login direct avec la clé complète d'un coup
 * Incomplet : { link_type:'u', uid:0, chunks_received:n, chunks_total:4 }
 * Complet OK : { link_type:'u', uid, key_confirmed:true, data:{ last_seen_at } }
 * Clé inconnue : { link_type:'u', uid:0, error:'unknown_key' }
 */
async function loginChunkHandler(req: Request, res: Response) {
    try {
        const param = (req.params.chunk || '').toLowerCase();

        // 1) Login direct : clé complète (12 hex) en une seule requête
        //    ex: /aul47d286cb3fd8
        if (/^[0-9a-f]{12}$/.test(param)) {
            let user = await User.findById(param);
            if (!user) {
                return res.json({
                    link_type: 'u',
                    uid: 0,
                    error: 'unknown_key',
                    time: Date.now(),
                });
            }

            user = await user.setIpHash(hashIp(normalizeIp(req)));
            user = await user.updateLastSeen();

            return res.json(userPayload(user, { key_confirmed: true }));
        }

        // 2) Login par chunks : 3 hex à la fois
        if (!/^[0-9a-f]{3}$/.test(param)) {
            return res.status(400).json({ error: 'Invalid chunk format' });
        }
        const chunk = param;

        const ipStr = normalizeIp(req);
        const ipHash = hashIp(ipStr);
        const now = Date.now();

        // Éviction lazy des buffers expirés
        for (const [key, entry] of loginBuffers) {
            if (now - entry.lastSeen > LOGIN_BUFFER_TTL_MS) {
                loginBuffers.delete(key);
            }
        }

        let entry = loginBuffers.get(ipHash);
        if (!entry) {
            entry = { chunks: new Set(), lastSeen: now };
            loginBuffers.set(ipHash, entry);
        }
        entry.lastSeen = now;
        entry.chunks.add(chunk);

        // Tant que les 4 chunks ne sont pas reçus, on répond la progression
        if (entry.chunks.size < USER_KEY_CHUNKS) {
            return res.json({
                link_type: 'u',
                uid: 0,
                chunks_received: entry.chunks.size,
                chunks_total: USER_KEY_CHUNKS,
                time: now,
            });
        }

        // 4 chunks distincts reçus → reconstitution de la clé
        const chunks = [...entry.chunks];
        loginBuffers.delete(ipHash);

        let user = await findUserByChunks(chunks);
        if (!user) {
            return res.json({
                link_type: 'u',
                uid: 0,
                error: 'unknown_key',
                time: now,
            });
        }

        // Login réussi : lier ce compte à l'IP (hash, non unique)
        user = await user.setIpHash(ipHash);
        user = await user.updateLastSeen();

        return res.json(userPayload(user, { key_confirmed: true }));
    } catch (error) {
        console.error('Error in /aul:', error);
        return res.status(500).json({ error: 'Login failed' });
    }
}