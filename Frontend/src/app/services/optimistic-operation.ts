export type OptimisticOperation = {
    wait: () => Promise<OptimisticOperationResult>;
}

export type OptimisticOperationResult =  OptimisticOperationSuccess | OptimisticOperationFailure;

export type OptimisticOperationSuccess = {
    type: 'success';
}

export type OptimisticOperationFailure = {
    type: 'failure';
    error: any;
}

export class Operations {
    public static optimistic(): OptimisticOperationImpl {
        return new OptimisticOperationImpl();
    };
}


export class OptimisticOperationImpl {
    private _promise: Promise<OptimisticOperationResult>
    private _resolve: (value: OptimisticOperationResult) => void = _ => {};

    constructor() {
        this._promise = new Promise((resolve, reject) => {
            this._resolve = resolve;
        });
    }

    wait(): Promise<OptimisticOperationResult> {
        return this._promise;
    }

    succeeded() {
        this._resolve({
            type: 'success'
        });
    }

    failed(error: any){
        this._resolve({
            type: 'failure',
            error: error
        });
    }
}