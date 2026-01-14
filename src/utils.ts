import { Request } from "express";

export function sleep(ms: number) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export function strToHash(str: string): number {
    let crc = 0xFFFFFFFF;
    for (let i = 0; i < str.length; i++) {
        const byte = str.charCodeAt(i);
        crc ^= byte;
        for (let j = 0; j < 8; j++) 
            crc = (crc >>> 1) ^ (0xEDB88320 & -(crc & 1));
    }
    return (crc ^ 0xFFFFFFFF);
}

export function ip(req: Request): string {
    console.log("Headers:", req.headers);
    return (req.headers["x-forwarded-for"] as string)?.split(",").shift() || req.socket.remoteAddress || req.ip?.toString() || "unknown";
}

export function uid(req: Request): number {
    return strToHash(ip(req));
}