import { Player as IPlayer } from "prisma";
import Instance from "@/database/Instance";
import Database from "@/database/Database";
import User from "@/database/User";

export default class Player implements IPlayer {
    public readonly instance_id: number;
    public readonly user_id: number;

    constructor(data: IPlayer) {
        this.instance_id = data.instance_id;
        this.user_id = data.user_id;
    }

    async getInstance(): Promise<Instance> {
        const instance = await Database.prisma.instance.findUnique({ where: { id: this.instance_id } });
        if (!instance) throw new Error("Instance not found");
        return new Instance(instance);
    }

    async getUser(): Promise<User> {
        const user = await Database.prisma.user.findUnique({ where: { id: this.user_id } });
        if (!user) throw new Error("User not found");
        return new User(user);
    }

    static async find(instanceId: number, userId: number) {
        let player = await Database.prisma.player.findUnique({
            where: {
                instance_id_user_id: {
                    instance_id: instanceId,
                    user_id: userId
                }
            }
        });
        return player ? new Player(player) : null;
    }


    static async create(instanceId: number, userId: number): Promise<Player> {
        const player = await Database.prisma.player.create({
            data: {
                instance_id: instanceId,
                user_id: userId
            }
        });
        return new Player(player);
    }
}