import { Config as IConfig } from 'prisma';
import Database from '@/database/Database';

export default class Config implements IConfig {
    public readonly key: string;
    public readonly value: string;

    constructor(data: IConfig) {
        this.key = data.key;
        this.value = data.value;
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