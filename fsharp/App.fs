module App

open Fable.Core
open System

// FUTHARK LOADING
[<Import("FutharkOpaque", from = "./app.fut.mjs")>]
type FutharkOpaque =
    member _.free() : unit = jsNative
    override this.Finalize() : unit = this.free ()

[<Import("FutharkArray", from = "./app.fut.mjs")>]
type FutharkArray<'a, 'b> =
    member _.toArray() : 'a [] = jsNative
    member _.toTypedArray() : 'b [] = jsNative
    member _.shape() : int [] = jsNative
    member _.free() : unit = jsNative

    interface IDisposable with
        member this.Dispose() = this.free ()

type WASM = obj

[<Import("FutharkContext", from = "./app.fut.mjs")>]
type FutharkContext =
    member _.free() : Unit = jsNative
    member _.new_u8_1d_from_jsarray(jsarray: uint8 []) : FutharkArray<uint8, uint8> = jsNative
    member _.new_u8_1d(jsarray: uint8 [], size: int) : FutharkArray<uint8, uint8> = jsNative
    member _.new_u32_1d_from_jsarray(jsarray: uint32 []) : FutharkArray<uint32, uint32> = jsNative
    member _.new_u32_1d(jsarray: uint32 [], size: int) : FutharkArray<uint32, uint32> = jsNative
    // Change this to the specific function implemented in the Futhark code
    member _.calc(x: int32, y: int32) : int32 = jsNative


type FutharkContextStatic =
    [<Emit("new $0($1)")>]
    abstract Create : WASM -> FutharkContext

[<Import("FutharkContext", from = "./app.fut.mjs")>]
let FutharkContext: FutharkContextStatic = jsNative

[<Import("loadWASM", from = "./app.fut.mjs")>]
let loadWASM: Unit -> WASM = jsNative

[<Import("newFutharkContext", from = "./app.fut.mjs")>]
let newFutharkContext: Unit -> JS.Promise<FutharkContext> = jsNative

// END

let ctx = FutharkContext.Create(loadWASM ())

[<EntryPoint>]
let main argv =
    Console.WriteLine(ctx.calc (4, 2))

    1
