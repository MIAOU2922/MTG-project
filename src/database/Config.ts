import Database from '@/database/Database';

export default class Config {
    public readonly key: string;
    public readonly value: string;
    public readonly created_at: Date;
    public readonly updated_at: Date;

    constructor(data: { key: string; value: string; created_at?: Date; updated_at?: Date }) {
        this.key = data.key;
        this.value = data.value;
        this.created_at = data.created_at || new Date();
        this.updated_at = data.updated_at || new Date();
    }

    public static async get(key: string, defaultValue: string): Promise<Config> {
        const config = await Database.prisma.config.findUnique({ where: { key } });
        return config ? new Config(config) : new Config({ key, value: defaultValue });
    }

    public static async set(key: string, value: string): Promise<Config> {
        const config = await Database.prisma.config.upsert({
            where: { key },
            update: { value },
            create: { key, value },
        });
        return new Config(config);
    }

    public async save(): Promise<void> {
        await Database.prisma.config.update({
            where: { key: this.key },
            data: { value: this.value },
        });
    }
}