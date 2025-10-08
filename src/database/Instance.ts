import { Instance as IInstance } from "prisma";
import Database from "@/database/Database";
import Player from "@/database/Player";
import User from "@/database/User";

export default class Instance implements IInstance {
    public static MAX_INSTANCES = 64;
    public readonly id: number;
    public readonly created_at: Date;
    public readonly last_seen_at: Date;

    constructor(data: IInstance) {
        this.id = data.id;
        this.created_at = data.created_at;
        this.last_seen_at = data.last_seen_at;
    }

    public static async findById(id: number): Promise<Instance | null> {
        const instance = await Database.prisma.instance.findUnique({ where: { id } });
        return instance ? new Instance(instance) : null;
    }

    public async getPlayers(): Promise<Player[]> {
        const players = await Database.prisma.player.findMany({ where: { instance_id: this.id } });
        return players.map(player => new Player(player));
    }

    public static async createWithRotation(user: User): Promise<Instance> {
        // Find the next available instance ID (0-63) using rotation
        let nextId = 0;
        
        // Get all existing instances and find the next ID
        const existingInstances = await Database.prisma.instance.findMany({
            orderBy: { id: 'asc' }
        });
        
        const usedIds = new Set(existingInstances.map(instance => instance.id));
        
        // Find first unused ID in range 0-63
        for (let i = 0; i <= Instance.MAX_INSTANCES - 1; i++) 
            if (!usedIds.has(i)) {
                nextId = i;
                break;
            }
        
        // If all IDs are used (64 instances), reuse the oldest one (rotation)
        if (usedIds.size >= Instance.MAX_INSTANCES) {
            // Find the oldest instance by created_at timestamp
            const oldestInstance = await Database.prisma.instance.findFirst({
                orderBy: { created_at: 'asc' }
            });
            
            if (oldestInstance) {
                nextId = oldestInstance.id;
                
                // Delete the old instance and its players
                await Database.prisma.player.deleteMany({ where: { instance_id: nextId } });
                await Database.prisma.instance.delete({ where: { id: nextId } });
            }
        }
        
        // Create new instance
        const instance = await Database.prisma.instance.create({
            data: { id: nextId }
        });
        
        return new Instance(instance);
    }
}