import { Request } from "express";

export function sleep(ms: number) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export function strToHash(str: string): number {
    let hash = 0;
    for (let i = 0; i < str.length; i++)
        hash = (hash * 31 + str.charCodeAt(i)) | 0;
    return hash;
}

export function ip(req: Request): string {
    return (req.headers["x-forwarded-for"] as string)?.split(",").shift() || req.socket.remoteAddress || req.ip?.toString() || "unknown";
}

export function uid(req: Request): number {
    return strToHash(ip(req));
}