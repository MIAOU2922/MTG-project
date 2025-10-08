import { User as IUser } from "prisma";
import Database from "@/database/Database";
import Player from "@/database/Player";

export default class User implements IUser {
    public readonly id: number;
    public readonly created_at: Date;
    public readonly last_seen_at: Date;

    constructor(data: IUser) {
        this.id = data.id;
        this.created_at = data.created_at;
        this.last_seen_at = data.last_seen_at;
    }

    public static async findById(id: number): Promise<User | null> {
        const user = await Database.prisma.user.findUnique({ where: { id } });
        return user ? new User(user) : null;
    }

    public static async findOrCreate(id: number): Promise<User> {
        let user = await Database.prisma.user.findUnique({ where: { id } });
        if (!user) user = await Database.prisma.user.create({ data: { id } });
        return new User(user);

    }

    public async getPlayers(): Promise<Player[]> {
        const players = await Database.prisma.player.findMany({ where: { user_id: this.id } });
        return players.map(player => new Player(player));
    }

    public async updateLastSeen(): Promise<User> {
        const user = await Database.prisma.user.update({
            where: { id: this.id },
            data: { last_seen_at: new Date() }
        });
        return new User(user);
    }
}
