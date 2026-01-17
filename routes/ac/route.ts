import { Request, Response, Router } from "express";
import Instance from "@/database/Instance";
import User from "@/database/User";
import { uid } from "@/utils";

export const apiCreateRouter = Router();
apiCreateRouter.use('/ac', acHandler);

async function acHandler(req: Request, res: Response) {
    try {
        const userId = uid(req);
        const user = await User.findOrCreate(userId);
        await user.updateLastSeen();

        // Create new instance with rotation
        const instance = await Instance.createWithRotation(user);

        // Add the user to the instance (removes from all other instances automatically)
        await instance.addUser(user.id);

        return res.json({
            link_type: 'c',
            link_id: '',
            iid: instance.id,
            uid: user.id,
            time: Date.now()
        });
    } catch (error) {
        console.error('Error in acHandler:', error);
        return res.status(500).json({ error: 'Error creating instance' });
    }
}