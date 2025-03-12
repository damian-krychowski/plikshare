import basex from 'base-x'
const BASE62="0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";


export function getBase62Guid() {
    const randomBytes = new Uint8Array(16);
    crypto.getRandomValues(randomBytes);
    
    // Convert to base62
    const base62 = basex(BASE62);
    const guid = base62.encode(randomBytes);

    return guid;
}

export function getBase62GuidFromBytes(bytes: Uint8Array) {
    if(bytes.length != 16) {
        throw new Error(`Wrong lenght of guid bytes array. Expected 16 bytes but found: ${bytes}`);
    }

    // Convert to base62
    const base62 = basex(BASE62);
    const guid = base62.encode(bytes);

    return guid;
}

export function getExternalIdFromBytes(prefix: string, bytes: Uint8Array){
    return `${prefix}${getBase62GuidFromBytes(bytes)}`;
}