import { Request, Response, Router } from "express";
import { apiUserRouter } from "@routes/au/route";
import { apiJoinRouter } from "@routes/aj/route";
import { apiCreateRouter } from "@routes/ac/route";
import { apiSearchRouter } from "@routes/as/route";
import { atRouter } from "@routes/at/route";
import { apiDeckRouter } from "@routes/ad/route";
import { apiLangRouter } from "@routes/al/route";
import Database from "@/database/Database";

export const router = Router();
router.use(apiUserRouter);
router.use(apiLangRouter);
router.use(apiJoinRouter);
router.use(apiCreateRouter);
router.use(apiSearchRouter);
router.use(atRouter);
router.use(apiDeckRouter);
router.get('/h', healthHandler);

/**
 * Health check : vérifie uniquement que l'API tourne et que la BDD répond.
 * Pas de résolution d'utilisateur — on teste l'infrastructure, pas les données.
 * - { status: 'ok' } si la BDD répond (l'API répond forcément, elle sert la réponse)
 * - { status: 'error' } avec HTTP 503 sinon
 */
async function healthHandler(req: Request, res: Response) {
    try {
        await Database.prisma.$queryRaw`SELECT 1`;
        res.json({ status: 'ok' });
    } catch (error) {
        console.error('Health check failed:', error);
        res.status(503).json({ status: 'error' });
    }
}