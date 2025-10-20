import ScryFallSync from "./sync/index";

/**
 * Main entry point for Scryfall bulk data synchronization
 * This script uses the new ScryFallSync class with streaming capabilities
 * 
 * Usage:
 * npm run sync           - Sync both cards and rulings
 * npm run sync cards     - Sync only cards
 * npm run sync rulings   - Sync only rulings
 * npm run sync cards 10500 - Sync cards starting from batch 210 (10500 / 50 cards per batch)
 * npm run sync 10500     - Sync both, starting from 10500th card
 */
async function main() {
  console.log("🃏 MTG Scryfall Sync Starting...");
  
  // Parse command line arguments
  const args = process.argv.slice(2);
  let syncType: string | undefined = args[0]?.toLowerCase();
  let startFromCard: number | undefined;
  
  let syncCards = true;
  let syncRulings = true;
  
  // Vérifier si le premier argument est un nombre (startFromCard)
  if (syncType && !isNaN(Number(syncType))) {
    startFromCard = Number(syncType);
    syncType = undefined;
    console.log(`🔄 Syncing BOTH cards and rulings (starting from card #${startFromCard})`);
  } else if (syncType === 'cards') {
    syncRulings = false;
    // Vérifier si le deuxième argument est un nombre
    if (args[1] && !isNaN(Number(args[1]))) {
      startFromCard = Number(args[1]);
      console.log(`📋 Syncing CARDS only (starting from card #${startFromCard})`);
    } else {
      console.log("📋 Syncing CARDS only");
    }
  } else if (syncType === 'rulings') {
    syncCards = false;
    // Vérifier si le deuxième argument est un nombre
    if (args[1] && !isNaN(Number(args[1]))) {
      startFromCard = Number(args[1]);
      console.log(`⚖️ Syncing RULINGS only (starting from card #${startFromCard})`);
    } else {
      console.log("⚖️ Syncing RULINGS only");
    }
  } else if (syncType && syncType !== 'all') {
    console.error("❌ Invalid option. Use: 'cards', 'rulings', a number, or no argument");
    console.log("Usage:");
    console.log("  npm run sync           - Sync both cards and rulings");
    console.log("  npm run sync cards     - Sync only cards");
    console.log("  npm run sync rulings   - Sync only rulings");
    console.log("  npm run sync 10500     - Sync both, starting from card #10500");
    console.log("  npm run sync cards 10500 - Sync cards only, starting from card #10500");
    process.exit(1);
  } else {
    console.log("🔄 Syncing BOTH cards and rulings");
  }
  
  try {
    const sync = new ScryFallSync();
    const metadata = await sync.start({ syncCards, syncRulings, startFromCard });
    
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
