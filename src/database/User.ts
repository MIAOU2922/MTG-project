import { User as IUser } from "prisma";
import Database from "@/database/Database";
import Instance from "@/database/Instance";

export default class User implements IUser {
    public readonly id: string;
    public readonly ip_hash: string | null;
    public readonly created_at: Date;
    public readonly last_seen_at: Date;

    constructor(data: IUser) {
        this.id = data.id;
        this.ip_hash = data.ip_hash;
        this.created_at = data.created_at;
        this.last_seen_at = data.last_seen_at;
    }

    public static async findById(id: string): Promise<User | null> {
        const user = await Database.prisma.user.findUnique({ where: { id } });
        return user ? new User(user) : null;
    }

    /**
     * Crée un compte dont l'id EST la clé (12 hex).
     * `ipHash` = hash SHA-256 de l'IP (jamais d'IP en clair).
     */
    public static async createWithKey(key: string, ipHash: string): Promise<User> {
        const user = await Database.prisma.user.create({ data: { id: key, ip_hash: ipHash } });
        return new User(user);
    }

    /**
     * Plusieurs users peuvent partager la même IP (même hash) :
     * on résout vers le plus récemment actif (dernier login gagnant).
     */
    public static async findMostRecentByIpHash(ipHash: string): Promise<User | null> {
        const user = await Database.prisma.user.findFirst({
            where: { ip_hash: ipHash },
            orderBy: { last_seen_at: 'desc' },
        });
        return user ? new User(user) : null;
    }

    /** Lie ce compte à une IP (hash) après un login par chunks réussi */
    public async setIpHash(ipHash: string): Promise<User> {
        const user = await Database.prisma.user.update({
            where: { id: this.id },
            data: { ip_hash: ipHash },
        });
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
