import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import * as protobuf from "protobufjs";
import { firstValueFrom } from "rxjs";
import { XSRF_TOKEN_HEADER_NAME } from "../shared/xsrf";
import { BOX_LINK_TOKEN_HEADER } from "./box-link-token.service";

export type ProtoStreamField = {
    name: string;
    repeated: boolean;
    type?: protobuf.Type;
    scalar?: 'uint32';
}

export class ProtoHttpError extends Error {
    constructor(
        message: string,
        public readonly status: number
    ) {
        super(message);
    }
}

function concatBytes(left: Uint8Array, right: Uint8Array): Uint8Array {
    if (left.length === 0)
        return right;

    const result = new Uint8Array(left.length + right.length);
    result.set(left, 0);
    result.set(right, left.length);

    return result;
}

function decodeAvailableFields(
    buffer: Uint8Array,
    fields: Record<number, ProtoStreamField>
): { chunk: Record<string, any> | null, bytesConsumed: number } {
    const reader = protobuf.Reader.create(buffer);

    let chunk: Record<string, any> | null = null;
    let bytesConsumed = 0;

    while (reader.pos < reader.len) {
        let tag: number;

        try {
            tag = reader.uint32();
        } catch {
            break;
        }

        const wireType = tag & 7;
        const field = fields[tag >>> 3];

        if (wireType === 0) {
            let value: number;

            try {
                value = reader.uint32();
            } catch {
                break;
            }

            if (field?.scalar) {
                chunk = chunk ?? {};
                chunk[field.name] = value;
            }
        } else if (wireType === 2) {
            let length: number;

            try {
                length = reader.uint32();
            } catch {
                break;
            }

            if (reader.pos + length > reader.len) {
                break;
            }

            if (field?.type) {
                const value = field.type.decode(reader, length);

                chunk = chunk ?? {};

                if (field.repeated) {
                    if (!chunk[field.name]) {
                        chunk[field.name] = [];
                    }

                    chunk[field.name].push(value);
                } else {
                    chunk[field.name] = value;
                }
            } else {
                reader.skip(length);
            }
        } else {
            throw new Error(`Unexpected wire type ${wireType} in streamed protobuf response`);
        }

        bytesConsumed = reader.pos;
    }

    return { chunk, bytesConsumed };
}

@Injectable({
    providedIn: 'root'
})
export class ProtoHttp {
    constructor(
        private _http: HttpClient) {
    }

    public async getStream(args: {
        route: string,
        fields: Record<number, ProtoStreamField>,
        onChunk: (chunk: Record<string, any>) => void,
        boxLinkToken?: string,
    }): Promise<void> {
        const headers: Record<string, string> = {
            'Accept': 'application/x-protobuf'
        };

        if (args.boxLinkToken) {
            headers[BOX_LINK_TOKEN_HEADER] = args.boxLinkToken;
        }

        const response = await fetch(args.route, {
            headers: headers,
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new ProtoHttpError(
                `Request to ${args.route} failed with status ${response.status}`,
                response.status);
        }

        if (!response.body) {
            throw new Error(`Request to ${args.route} returned no body`);
        }

        const reader = response.body.getReader();
        let pending: Uint8Array = new Uint8Array(0);

        while (true) {
            const { done, value } = await reader.read();

            if (value && value.length > 0) {
                pending = concatBytes(pending, value);

                const { chunk, bytesConsumed } = decodeAvailableFields(
                    pending,
                    args.fields);

                if (bytesConsumed > 0) {
                    pending = pending.subarray(bytesConsumed);
                }

                if (chunk) {
                    args.onChunk(chunk);
                }
            }

            if (done) {
                break;
            }
        }

        if (pending.length > 0) {
            throw new Error(`Response from ${args.route} ended with an incomplete protobuf field`);
        }
    }

    public async get<TResponse>(args: { 
        route: string, 
        responseProtoType: protobuf.Type
        boxLinkToken?: string,
    }): Promise<TResponse> {
        let headers = new HttpHeaders({
            'Accept': 'application/x-protobuf',
            'Accept-Encoding': 'gzip' 
        });

        if(args.boxLinkToken) {
            headers = headers.set(BOX_LINK_TOKEN_HEADER, args.boxLinkToken);
        }

        const call = this._http.get(args.route,{
                responseType: 'arraybuffer',
                headers: headers
            }
        );
    
        const compressedData: ArrayBuffer = await firstValueFrom(call);        
        const reader = protobuf.Reader.create(new Uint8Array(compressedData));
        const result: TResponse = args.responseProtoType.decode(reader) as any;

        return result;
    }

    public async post<TRequest, TResponse>(
        args: { 
            route: string, 
            request: TRequest,
            requestProtoType: protobuf.Type, 
            responseProtoType: protobuf.Type,
            xsrfToken?: string,
            boxLinkToken?: string,
        }
    ): Promise<TResponse> {

        const requestBuffer = args.requestProtoType.encode(args.request as any).finish();
        const blob = new Blob([new Uint8Array(requestBuffer)], { type: 'application/x-protobuf' });
        
        let headers = new HttpHeaders({
            'Content-Type': 'application/x-protobuf',
            'Accept': 'application/x-protobuf',
            'Accept-Encoding': 'gzip'
        });

        if (args.xsrfToken) {
            headers = headers.set(XSRF_TOKEN_HEADER_NAME, args.xsrfToken);
        }

        if(args.boxLinkToken) {
            headers = headers.set(BOX_LINK_TOKEN_HEADER, args.boxLinkToken);
        }

        const call = this._http.post(args.route, blob, {
            responseType: 'arraybuffer',
            headers: headers
        });

        // Handle response
        const compressedData: ArrayBuffer = await firstValueFrom(call);
        const reader = protobuf.Reader.create(new Uint8Array(compressedData));
        const result: TResponse = args.responseProtoType.decode(reader) as any;

        return result;
    }

    public async postJsonToProto<TRequest, TResponse>(
        args: { 
            route: string, 
            request: TRequest,
            responseProtoType: protobuf.Type ,
            xsrfToken?: string,
            boxLinkToken?: string,
        }
    ): Promise<TResponse> {      
        let headers = new HttpHeaders({
            'Content-Type': 'application/json',
            'Accept': 'application/x-protobuf',
            'Accept-Encoding': 'gzip'
        });
        
        if (args.xsrfToken) {
            headers = headers.set(XSRF_TOKEN_HEADER_NAME, args.xsrfToken);
        }
        
        if(args.boxLinkToken) {
            headers = headers.set(BOX_LINK_TOKEN_HEADER, args.boxLinkToken);
        }

        const call = this._http.post(args.route, args.request, {
            responseType: 'arraybuffer',
            headers: headers
        });

        // Handle response
        const compressedData: ArrayBuffer = await firstValueFrom(call);
        const reader = protobuf.Reader.create(new Uint8Array(compressedData));
        const result: TResponse = args.responseProtoType.decode(reader) as any;

        return result;
    }
}