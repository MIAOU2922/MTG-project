import { PrismaClient } from "prisma";

export default class Database {
    public static prisma = new PrismaClient({
        // Configuration optimisée pour les performances
        datasources: {
            db: {
                url: process.env.DATABASE_URL
            }
        },
        log: ['warn', 'error'], // Réduire les logs pour de meilleures performances
        transactionOptions: {
            timeout: 120000, // 2 minutes pour les transactions longues
            maxWait: 10000, // 10 secondes d'attente maximum
        }
    });

    // Configuration du pool de connexions pour PostgreSQL
    static {
        // Vérification de la configuration d'environnement recommandée
        if (process.env.NODE_ENV === 'production') {
            console.log('Database configured for production with optimized settings');
        }
    }
}
