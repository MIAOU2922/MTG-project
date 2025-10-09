import { User as IUser } from "prisma";
import Database from "@/database/Database";
import Instance from "@/database/Instance";

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

    public async getInstance(): Promise<Instance | null> {
        const instance = await Database.prisma.instance.findFirst({
            where: { user_ids: { has: this.id } }
        });
        return instance ? new Instance(instance) : null;
    }

    public async updateLastSeen(): Promise<User> {
        const user = await Database.prisma.user.update({
            where: { id: this.id },
            data: { last_seen_at: new Date() }
        });

        // Mettre à jour aussi l'instance si l'utilisateur en a une
        try {
            const instance = await this.getInstance();
            if (instance) {
                await instance.updateLastSeen();
            }
        } catch (error) {
            // Ne pas échouer si la mise à jour de l'instance échoue
            console.warn(`Failed to update instance last_seen for user ${this.id}:`, error);
        }

        return new User(user);
    }
}
