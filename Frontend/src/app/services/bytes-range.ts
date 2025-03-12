
///start: index of first byte in range
///end: index of last byte in range 
///(note - be careful in JS as in blob slicing an end means first byte not in the slice, so byterange.end + 1)
export type BytesRange = {
    start: number;
    end: number;
}