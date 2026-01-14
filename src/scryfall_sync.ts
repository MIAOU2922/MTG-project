import ScryFallSync from "./sync/index";

/**
 * Main entry point for Scryfall bulk data synchronization
 * This script uses the new ScryFallSync class with streaming capabilities and concurrent processing
 * 
 * Usage:
 * npm run sync                    - Sync both cards and rulings (concurrency: 5)
 * npm run sync cards              - Sync only cards
 * npm run sync rulings            - Sync only rulings
 * npm run sync 10                 - Sync both with concurrency of 10
 * npm run sync cards 10           - Sync cards with concurrency of 10
 */
async function main() {
  console.log("🃏 MTG Scryfall Sync Starting...");
  
  // Parse command line arguments
  const args = process.argv.slice(2);
  let syncType: string | undefined = args[0]?.toLowerCase();
  let concurrency = 5; // Défaut
  
  let syncCards = true;
  let syncRulings = true;
  
  // Vérifier si le premier argument est un nombre (concurrency)
  if (syncType && !isNaN(Number(syncType))) {
    concurrency = Number(syncType);
    syncType = undefined;
    console.log(`🔄 Syncing BOTH cards and rulings (concurrency: ${concurrency})`);
  } else if (syncType === 'cards') {
    syncRulings = false;
    // Vérifier si le deuxième argument est un nombre
    if (args[1] && !isNaN(Number(args[1]))) {
      concurrency = Number(args[1]);
      console.log(`📋 Syncing CARDS only (concurrency: ${concurrency})`);
    } else {
      console.log("📋 Syncing CARDS only");
    }
  } else if (syncType === 'rulings') {
    syncCards = false;
    // Vérifier si le deuxième argument est un nombre
    if (args[1] && !isNaN(Number(args[1]))) {
      concurrency = Number(args[1]);
      console.log(`⚖️ Syncing RULINGS only (concurrency: ${concurrency})`);
    } else {
      console.log("⚖️ Syncing RULINGS only");
    }
  } else if (syncType && syncType !== 'all') {
    console.error("❌ Invalid option. Use: 'cards', 'rulings', a number, or no argument");
    console.log("Usage:");
    console.log("  npm run sync                    - Sync both cards and rulings (concurrency: 5)");
    console.log("  npm run sync cards              - Sync only cards");
    console.log("  npm run sync rulings            - Sync only rulings");
    console.log("  npm run sync 10                 - Sync both with concurrency of 10");
    console.log("  npm run sync cards 10           - Sync cards with concurrency of 10");
    process.exit(1);
  } else {
    console.log("🔄 Syncing BOTH cards and rulings");
  }
  
  try {
    const sync = new ScryFallSync(concurrency);
    await sync.start({ syncCards, syncRulings });
    
    console.log("✅ Sync completed successfully!");
    process.exit(0);
  } catch (error) {
    console.error("❌ Sync failed:", error);
    process.exit(1);
  }
}

// Run if this file is executed directly
if (require.main === module) {
  main().catch(console.error);
}
