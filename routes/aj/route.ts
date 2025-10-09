import { Request, Response, Router } from "express";
import Instance from "@/database/Instance";
import User from "@/database/User";
import { uid } from "@/utils";

export const apiJoinRouter = Router();
apiJoinRouter.get('/aj:instanceCode', ajHandler);

async function removeUserFromOldInstances(userId: number): Promise<void> {
    // Supprimer l'utilisateur de toutes ses instances précédentes
    const instances = await Instance.findById(0); // On va chercher toutes les instances
    // En fait, on doit chercher toutes les instances qui contiennent cet userId
    const allInstances = await Instance.findById(0); // Cette approche n'est pas bonne

    // Mieux : chercher toutes les instances et vérifier lesquelles contiennent l'userId
    // Pour l'instant, on va faire une approche simple : chercher l'instance actuelle de l'utilisateur
    const user = await User.findById(userId);
    if (user) {
        const currentInstance = await user.getInstance();
        if (currentInstance) {
            await currentInstance.removeUser(userId);
        }
    }
}

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

        // Vérifier si l'utilisateur est déjà dans cette instance
        if (!instance.hasUser(user.id)) {
            // Supprimer l'utilisateur de ses anciennes instances avant de le placer dans la nouvelle
            await removeUserFromOldInstances(user.id);
            await instance.addUser(user.id);
        }

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