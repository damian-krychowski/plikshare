import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import * as protobuf from "protobufjs";
import { firstValueFrom } from "rxjs";
import { XSRF_TOKEN_HEADER_NAME } from "../shared/xsrf";
import { BOX_LINK_TOKEN_HEADER } from "./box-link-token.service";

@Injectable({
    providedIn: 'root'
})
export class ProtoHttp {
    constructor(
        private _http: HttpClient) {        
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
        const blob = new Blob([requestBuffer], { type: 'application/x-protobuf' });
        
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