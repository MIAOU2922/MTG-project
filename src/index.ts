import Main from '@/Main';
import Dotenv from 'dotenv';

// Load environment variables from .env file
Dotenv.config();

// Create and start the server
const server = Main.getInstance();

// Listen for server events
server.on('server:started', (data) => {
    console.log(`✅ Server successfully started on port ${data.port}`);
});

// Handle graceful shutdown
process.on('SIGTERM', () => {
    console.log('🛑 SIGTERM received. Shutting down gracefully...');
    process.exit(0);
});

process.on('SIGINT', () => {
    console.log('🛑 SIGINT received. Shutting down gracefully...');
    process.exit(0);
});

// Start the server
server.start();