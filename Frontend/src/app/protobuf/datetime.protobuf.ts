// import * as protobuf from "protobufjs";

// export function getDateTimeProtobuf() {
//     const dateTimeType = new protobuf.Type("appDateTime")
//         .add(new protobuf.Field("milliseconds", 1, "int64"))
//         .add(new protobuf.Field("nanos", 2, "int32"));

//     // Add custom decode method
//     const originalDecode = dateTimeType.decode.bind(dateTimeType);
//     (dateTimeType.decode as any) = function(reader: protobuf.Reader | Uint8Array, length?: number) {
//         // First decode using the original decoder
//         const message = originalDecode(reader, length) as any as ProtoDateTime;
        
//         // Convert to ISO string
//         const millis = typeof message.milliseconds === 'number' 
//             ? message.milliseconds 
//             : (message.milliseconds as any).toNumber();
            
//         const milliseconds = millis + Math.floor(message.nanos / 1_000_000);
//         const isoString = new Date(milliseconds).toISOString();

//         // Return enhanced message with ISO string
//         return {
//             ...message,
//             isoString
//         };
//     };

//     return dateTimeType;
// }

// export interface ProtoDateTime {
//     milliseconds: number;
//     nanos: number;
// }

import * as protobuf from "protobufjs";

// Define the enums first
export enum TimeSpanScale {
    DAYS = 0,
    HOURS = 1,
    MINUTES = 2,
    SECONDS = 3,
    MILLISECONDS = 4,
    TICKS = 5,
    MINMAX = 15
}

export enum DateTimeKind {
    UNSPECIFIED = 0,
    UTC = 1,
    LOCAL = 2
}

export interface ProtoDateTime {
    value: number;
    scale: TimeSpanScale;
    kind: DateTimeKind;
}

export function getDateTimeProtobuf() {
    // Create enum types
    const timeSpanScaleEnum = new protobuf.Enum("TimeSpanScale", {
        DAYS: 0,
        HOURS: 1,
        MINUTES: 2,
        SECONDS: 3,
        MILLISECONDS: 4,
        TICKS: 5,
        MINMAX: 15
    });

    const dateTimeKindEnum = new protobuf.Enum("DateTimeKind", {
        UNSPECIFIED: 0,
        UTC: 1,
        LOCAL: 2
    });

    // Create the main DateTime type
    const dateTimeType = new protobuf.Type("appDateTime")
        .add(new protobuf.Field("value", 1, "sint64"))  // Using sint64 as specified
        .add(new protobuf.Field("scale", 2, "TimeSpanScale"))
        .add(new protobuf.Field("kind", 3, "DateTimeKind"))
        .add(timeSpanScaleEnum)
        .add(dateTimeKindEnum);

    // Add custom decode method
    const originalDecode = dateTimeType.decode.bind(dateTimeType);
    (dateTimeType.decode as any) = function(reader: protobuf.Reader | Uint8Array, length?: number) {
        // First decode using the original decoder
        const message = originalDecode(reader, length) as any as ProtoDateTime;
        
        // Convert the value based on scale
        const baseValue = typeof message.value === 'number' 
            ? message.value 
            : (message.value as any).toNumber();

        let milliseconds: number;
        switch (message.scale) {
            case TimeSpanScale.DAYS:
                milliseconds = baseValue * 24 * 60 * 60 * 1000;
                break;
            case TimeSpanScale.HOURS:
                milliseconds = baseValue * 60 * 60 * 1000;
                break;
            case TimeSpanScale.MINUTES:
                milliseconds = baseValue * 60 * 1000;
                break;
            case TimeSpanScale.SECONDS:
                milliseconds = baseValue * 1000;
                break;
            case TimeSpanScale.MILLISECONDS:
                milliseconds = baseValue;
                break;
            case TimeSpanScale.TICKS:
                milliseconds = Math.floor(baseValue / 10000); // Convert .NET ticks (100-nanosecond intervals) to milliseconds
                break;
            default:
                milliseconds = baseValue;
        }

        const date = new Date(milliseconds);

        return date.toISOString();
    };

    return dateTimeType;
}