import ScryFallSync from "./sync/index";

/**
 * Main entry point for Scryfall bulk data synchronization
 * This script uses the new ScryFallSync class with streaming capabilities
 */
async function main() {
  console.log("🃏 MTG Scryfall Sync Starting...");
  
  try {
    const sync = new ScryFallSync();
    const metadata = await sync.start();
    
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
