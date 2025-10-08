import { Request, Response, Router } from "express";
import { apiUserRouter } from "@routes/au/route";
import { apiJoinRouter } from "@routes/aj/route";
import { apiCreateRouter } from "@routes/ac/route";
import { apiSearchRouter } from "@routes/as/route";
import { uid } from "@/utils";

export const router = Router();
router.use(apiUserRouter);
router.use(apiJoinRouter);
router.use(apiCreateRouter);
router.use(apiSearchRouter);
router.get('/h', healthHandler);

function healthHandler(req: Request, res: Response) {
    res.json({
        status: 'ok',
        uid: uid(req)
    });
}