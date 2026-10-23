import User from '@/database/User';
import { Router, Request, Response } from 'express';
import { getUserId } from '@/utils';

export const apiLangRouter = Router();

/**
 * Langues acceptées (codes Scryfall). Udon ne peut pas construire d'URLs
 * dynamiques : le client utilise des liens statiques du type /alen, /alfr…
 */
export const SUPPORTED_LANGS = [
    'en', 'fr', 'it', 'es', 'de', 'pt',
    'ja', 'ko', 'ru', 'zhs', 'zht',
    'he', 'la', 'grc', 'ar', 'sa', 'px',
] as const;

export type LangCode = typeof SUPPORTED_LANGS[number];

// GET /al : langue actuelle de l'utilisateur + langues supportées
apiLangRouter.get('/al', getLangHandler);
// GET /al{code} : définit la langue de l'utilisateur (ex: /alfr, /alen, /alit)
apiLangRouter.get('/al:code', setLangHandler);

function langPayload(user: User, lang: string, extra: Record<string, unknown> = {}) {
    return {
        link_type: 'l', // l = lang
        link_id: lang,
        iid: null,
        uid: user.id,
        time: Date.now(),
        data: { lang },
        ...extra,
    };
}

/**
 * GET /al — retourne la langue actuelle de l'utilisateur et la liste
 * des langues supportées.
 */
async function getLangHandler(req: Request, res: Response) {
    try {
        const userId = await getUserId(req);
        if (userId === null) {
            return res.status(401).json({ error: 'unknown_user', hint: 'Register via /aur first' });
        }
        const user = await User.findById(userId);
        if (!user) {
            return res.status(401).json({ error: 'unknown_user' });
        }
        await user.updateLastSeen();

        return res.json(langPayload(user, user.lang, { supported: [...SUPPORTED_LANGS] }));
    } catch (error) {
        console.error('Error in /al:', error);
        return res.status(500).json({ error: 'Failed to get language' });
    }
}

/**
 * GET /al{code} — définit la langue préférée de l'utilisateur.
 * La langue est liée au USER (pas à l'instance).
 */
async function setLangHandler(req: Request, res: Response) {
    try {
        const code = (req.params.code || '').toLowerCase();

        if (!(SUPPORTED_LANGS as readonly string[]).includes(code)) {
            return res.status(400).json({
                error: 'unsupported_language',
                supported: [...SUPPORTED_LANGS],
            });
        }

        const userId = await getUserId(req);
        if (userId === null) {
            return res.status(401).json({ error: 'unknown_user', hint: 'Register via /aur first' });
        }
        let user = await User.findById(userId);
        if (!user) {
            return res.status(401).json({ error: 'unknown_user' });
        }

        user = await user.setLang(code);
        user = await user.updateLastSeen();

        return res.json(langPayload(user, user.lang));
    } catch (error) {
        console.error('Error in /al{code}:', error);
        return res.status(500).json({ error: 'Failed to set language' });
    }
}
