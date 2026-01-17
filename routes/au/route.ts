import User from '@/database/User';
import { strToHash } from '@/utils';
import { Router, Request, Response } from 'express';

export const apiUserRouter = Router();
apiUserRouter.use('/au', handler);

async function handler(req: Request, res: Response) {
    let user = await User.findOrCreate(strToHash(req.ip?.toString() || 'unknown'));
    user = await user.updateLastSeen();
    return res.json({
        link_type: 'u',
        link_id: '',
        iid: null,
        uid: user.id,
        time: Date.now(),
        data: {
            last_seen_at: user.last_seen_at.getTime()
        }
    });
}