import ScryFallSync from "./sync/index";

/**
 * Main entry point for Scryfall bulk data synchronization
 * This script uses the new ScryFallSync class with streaming capabilities
 * 
 * Usage:
 * npm run sync           - Sync both cards and rulings
 * npm run sync cards     - Sync only cards
 * npm run sync rulings   - Sync only rulings
 */
async function main() {
  console.log("🃏 MTG Scryfall Sync Starting...");
  
  // Parse command line arguments
  const args = process.argv.slice(2);
  const syncType = args[0]?.toLowerCase();
  
  let syncCards = true;
  let syncRulings = true;
  
  if (syncType === 'cards') {
    syncRulings = false;
    console.log("📋 Syncing CARDS only");
  } else if (syncType === 'rulings') {
    syncCards = false;
    console.log("⚖️ Syncing RULINGS only");
  } else if (syncType && syncType !== 'all') {
    console.error("❌ Invalid option. Use: 'cards', 'rulings', or no argument for both");
    console.log("Usage:");
    console.log("  npm run sync           - Sync both cards and rulings");
    console.log("  npm run sync cards     - Sync only cards");
    console.log("  npm run sync rulings   - Sync only rulings");
    process.exit(1);
  } else {
    console.log("🔄 Syncing BOTH cards and rulings");
  }
  
  try {
    const sync = new ScryFallSync();
    const metadata = await sync.start({ syncCards, syncRulings });
    
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
