#!/usr/bin/env node

import Instance from "./database/Instance";
import Database from "./database/Database";

async function main() {
    try {
        console.log('🚀 Starting automatic image download for recent instances...');

        // Télécharger les images des instances récentes
        await Instance.downloadRecentInstanceImages();

        console.log('✅ Automatic image download completed successfully');
        process.exit(0);
    } catch (error) {
        console.error('❌ Error during automatic image download:', error);
        process.exit(1);
    }
}

main();