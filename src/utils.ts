import { Request } from "express";
import crypto from "crypto";
import User from "@/database/User";

/** Longueur de la clé utilisateur en caractères hex */
export const USER_KEY_LENGTH = 12;
/** Nombre de chunks de login (Udon ne peut pas construire d'URL dynamiques) */
export const USER_KEY_CHUNKS = 4;
/** Longueur d'un chunk en caractères hex */
export const USER_KEY_CHUNK_LENGTH = USER_KEY_LENGTH / USER_KEY_CHUNKS;

export function sleep(ms: number) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export function normalizeIp(req: Request): string {
    const fwd = req.headers["x-forwarded-for"];
    const forwarded = Array.isArray(fwd) ? fwd[0] : (fwd as string)?.split(",").shift();
    return forwarded || req.socket.remoteAddress || req.ip?.toString() || "unknown";
}

/**
 * Hash SHA-256 de l'IP : l'IP n'est JAMAIS stockée en clair.
 * (utilisé pour la résolution IP → user et pour la colonne users.ip_hash)
 */
export function hashIp(ipStr: string): string {
    return crypto.createHash('sha256').update(ipStr).digest('hex');
}

// =============================================================================
// SYSTÈME D'IDENTITÉ (id non lié à l'IP)
// =============================================================================

/**
 * Génère une clé aléatoire de 12 hex (48 bits) avec 4 chunks de 3 hex
 * tous distincts (le login par chunks ne transmet pas l'ordre, la clé est
 * reconstituée par permutation — des chunks identiques rendraient la
 * reconstitution ambiguë).
 */
export function generateUserKey(): string {
    for (let attempt = 0; attempt < 100; attempt++) {
        const key = crypto.randomBytes(USER_KEY_LENGTH / 2).toString('hex');
        const chunks = splitKeyIntoChunks(key);
        if (new Set(chunks).size === chunks.length) return key;
    }
    throw new Error('Unable to generate user key with distinct chunks');
}

export function splitKeyIntoChunks(key: string): string[] {
    const chunks: string[] = [];
    for (let i = 0; i < USER_KEY_CHUNKS; i++) {
        chunks.push(key.substring(i * USER_KEY_CHUNK_LENGTH, (i + 1) * USER_KEY_CHUNK_LENGTH));
    }
    return chunks;
}

/**
 * Résout l'utilisateur d'une requête via la colonne `ip` de la table users.
 * Plusieurs users peuvent partager une IP → le plus récemment actif gagne.
 * Retourne null si l'IP est inconnue (le client doit se register/login
 * via /aur ou /aul{3hex} au préalable).
 */
export async function getUserId(req: Request): Promise<string | null> {
    const ipStr = normalizeIp(req);
    const user = await User.findMostRecentByIpHash(hashIp(ipStr));
    return user ? user.id : null;
}