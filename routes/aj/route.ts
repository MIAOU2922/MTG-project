import { Request, Response, Router } from "express";
import Instance from "@/database/Instance";
import User from "@/database/User";
import { uid } from "@/utils";
import Player from "@/database/Player";

export const apiJoinRouter = Router();
apiJoinRouter.get('/aj:instanceCode', ajHandler);

async function ajHandler(req: Request, res: Response) {
    try {
        const { instanceCode } = req.params;
        const instanceId = parseInt(instanceCode, 36);

        if (isNaN(instanceId) || instanceId < 0 || instanceId > Instance.MAX_INSTANCES - 1)
            return res.status(400).json({
                error: `Invalid instance ID (must be between 0 and ${Instance.MAX_INSTANCES - 1})`
            });

        const userId = uid(req);
        const user = await User.findOrCreate(userId);
        await user.updateLastSeen();

        const instance = await Instance.findById(instanceId);
        if (!instance)
            return res.status(404).json({
                error: 'Instance not found'
            });

        let existingPlayer: Player | null = await Player.find(instanceId, user.id);
        if (!existingPlayer)
            existingPlayer = await Player.create(instanceId, user.id);

        if (!existingPlayer)
            return res.status(500).json({ error: 'Failed to create player' });

        return res.json({
            time: Date.now(),
            uid: user.id,
            iid: instance.id
        });
    } catch (error) {
        console.error('Error in ajHandler:', error);
        return res.status(500).json({
            error: 'Error joining instance'
        });
    }
}