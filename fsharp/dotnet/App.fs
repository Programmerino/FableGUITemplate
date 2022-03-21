module App

open Fable.Core
open System
open Browser
open Browser.Types
open Elmish
open Elmish.React
open Zanaptak.TypedCssClasses
open Fable.Core.JsInterop
open Elmish.Debug
open Feliz
open Elmish.HMR
open Fss
open FSharpPlus.Operators


// FUTHARK LOADING
[<Import("FutharkOpaque", from = "${outDir}/app.fut.mjs")>]
type FutharkOpaque =
    member _.free() : unit = jsNative
    override this.Finalize() : unit = this.free ()

[<Import("FutharkArray", from = "${outDir}/app.fut.mjs")>]
type FutharkArray<'a, 'b> =
    member _.toArray() : 'a [] = jsNative
    member _.toTypedArray() : 'b [] = jsNative
    member _.shape() : int [] = jsNative
    member _.free() : unit = jsNative

    interface IDisposable with
        member this.Dispose() = this.free ()

type WASM = obj

[<Import("FutharkContext", from = "${outDir}/app.fut.mjs")>]
type FutharkContext =
    member _.free() : Unit = jsNative
    member _.new_u8_1d_from_jsarray(jsarray: uint8 []) : FutharkArray<uint8, uint8> = jsNative
    member _.new_u8_1d(jsarray: uint8 [], size: int) : FutharkArray<uint8, uint8> = jsNative
    member _.new_u32_1d_from_jsarray(jsarray: uint32 []) : FutharkArray<uint32, uint32> = jsNative
    member _.new_u32_1d(jsarray: uint32 [], size: int) : FutharkArray<uint32, uint32> = jsNative
    // Change this to the specific function implemented in the Futhark code
    member _.calc(screenX: int64, screenY: int64, depth: int32, xmin: float32, ymin: float32, xmax: float32, ymax: float32) : FutharkArray<FutharkArray<int32, int32>, int32> = jsNative


type FutharkContextStatic =
    [<Emit("new $0($1)")>]
    abstract Create : WASM -> FutharkContext

[<Import("FutharkContext", from = "${outDir}/app.fut.mjs")>]
let FutharkContext: FutharkContextStatic = jsNative

[<Import("newFutharkContext", from = "${outDir}/app.fut.mjs")>]
let newFutharkContext: Unit -> JS.Promise<FutharkContext> = jsNative

// END

importSideEffects("@picocss/pico/css/pico.min.css")
type Pico = CssClasses<"../deps/node_modules/@picocss/pico/css/pico.min.css">

let mutable ctx = Unchecked.defaultof<FutharkContext>

type Model = 
  { IsJustLoaded: bool; Width: int64; Height: int64; Depth: int32; Xmin: float; Ymin: float; Xmax: float; Ymax: float; MouseX: float; MouseY: float }

type Msg =
| CanvasResize of (int * int)
| CalcResult
| CalcError of string
| DepthChange of int32
| XminChange of float
| YminChange of float
| XmaxChange of float
| YmaxChange of float
| MouseMove of (float * float)
| InitialLoad
| Reset

let getCanvasSize () = let elem = document.getElementById("mandelCanvas") :?> HTMLCanvasElement in CanvasResize(elem.getBoundingClientRect().width |> int, elem.getBoundingClientRect().height |> int)

let init() =
  { IsJustLoaded = true; Width = 0; Height = 0; Depth = 255; Xmin = -2.23; Ymin = -1.15; Xmax = 0.83; Ymax = 1.15; MouseX = 0.0; MouseY = 0.0 }, Cmd.none

[<Emit "new ImageData($0...)">]
let createImageData (data: JS.Uint8ClampedArray) (width: float) (height: float): ImageData = jsNative

let calcWrapper (screenX: int64, screenY: int64, depth: int32, xmin: float32, ymin: float32, xmax: float32, ymax: float32) =
  let value = ctx.calc (screenX, screenY, depth, xmin, ymin, xmax, ymax)
  let vals = value.toTypedArray()
  let data = JS.Constructors.Uint8ClampedArray.Create(vals?length * 4)
  for i = 0 to (vals?length - 1) do
    data[4*i+0] <- uint8(vals[i] &&& 0xFF0000) >>> 16
    data[4*i+1] <- uint8(vals[i] &&& 0xFF00) >>> 8
    data[4*i+2] <- uint8(vals[i] &&& 0xFF)
    data[4*i+3] <- 255uy
  value.free()
  let canvas = document.getElementById("mandelCanvas") :?> HTMLCanvasElement
  canvas.width <- screenX |> float
  canvas.height <- screenY |> float
  let ctx = canvas.getContext("2d") :?> CanvasRenderingContext2D
  let imgdata: ImageData = createImageData data (screenX |> float) (screenY |> float)
  ctx.putImageData(imgdata, 0, 0)

let calcCmd model = model, Cmd.OfFunc.either calcWrapper (model.Width, model.Height, model.Depth, model.Xmin |> float32, model.Ymin |> float32, model.Xmax |> float32, model.Ymax |> float32) (konst CalcResult) (fun e -> CalcError $"{e}")

let update (msg: Msg) (model: Model) =
    match msg with
    | CanvasResize(width, height) ->
      calcCmd { model with Width = width; Height = height }
    | CalcError(e) -> printfn $"Error: {e}"; model, Cmd.none
    | CalcResult -> model, Cmd.none
    | DepthChange(depth) ->
      calcCmd { model with Depth = depth }
    | XminChange(xmin) ->
      calcCmd { model with Xmin = xmin } 
    | YminChange(ymin) ->
      calcCmd { model with Ymin = ymin }
    | XmaxChange(xmax) ->
      calcCmd { model with Xmax = xmax }
    | YmaxChange(ymax) -> 
      calcCmd { model with Ymax = ymax }
    | MouseMove (x, y) -> 
      let canvas = document.getElementById("mandelCanvas") :?> HTMLCanvasElement
      let rect = canvas.getBoundingClientRect()
      let x = (((x - rect.left) / rect.width)) * (model.Xmax - model.Xmin) + model.Xmin
      let y = (1.0 - ((y - rect.top) / rect.height)) * (model.Ymax - model.Ymin) + model.Ymin
      {model with MouseX = x; MouseY = y}, Cmd.none
    | Reset ->
      (fst (init())), Cmd.ofMsg (getCanvasSize())
    | InitialLoad ->
      { model with IsJustLoaded = false }, Cmd.ofMsg (getCanvasSize())


let inline simpleControl (dispatch: Msg -> unit) name modelVal msg =
  Html.label [
    prop.htmlFor name
    prop.children [
      Html.text name
      Html.input [
        prop.type'.number
        prop.onInput (fun x -> dispatch (msg((x.target :?> HTMLInputElement).value)))
        prop.name name
        modelVal
        prop.required true
      ]
    ]
  ]

// (screenX: i64) (screenY: i64) (depth: i32)
//          (xmin: f32) (ymin: f32) (xmax: f32) (ymax: f32): [screenY][screenX]i32
let view (model :Model) dispatch =
    let simpleControl = simpleControl dispatch
    Html.main [
      prop.className Pico.``container-fluid``
      prop.fss [
        Display.flex
        FlexDirection.column
        Height.value (pct 100)
      ]
      prop.children [
        Html.div [
          prop.fss [
            Display.flex
            FlexDirection.column
            AlignItems.center
          ]
          prop.children [
            Html.h1 "FableGUITemplater"
          ]
        ]
        Html.form [
          prop.children [
            Html.div [
              prop.className Pico.``grid``
              prop.children [
                simpleControl "Depth" (prop.value model.Depth) (fun x -> DepthChange (int x))
                simpleControl "XMin" (prop.value model.Xmin) (fun x -> XminChange (float x))
                simpleControl "YMin" (prop.value model.Ymin) (fun x -> YminChange (float x))
                simpleControl "XMax" (prop.value model.Xmax) (fun x -> XmaxChange (float x))
                simpleControl "YMax" (prop.value model.Ymax) (fun x -> YmaxChange (float x))
              ]
            ]
            Html.button [
              prop.type'.button
              prop.onClick (fun _ -> dispatch Reset)
              prop.text "Reset"
            ]
          ]
        ]
        Html.p $"({model.MouseX}, {model.MouseY})"
        Html.canvas [
          prop.id "mandelCanvas"
          prop.onMouseMove (fun x -> dispatch (MouseMove (x.clientX, x.clientY)))
          prop.fss [
            Left.value (px 0)
            Width.value (vw 100)
            Height.value (pct 100)
          ]
          prop.ref (fun element ->
              if not (isNull element) then
                  if model.IsJustLoaded then
                      dispatch InitialLoad
          )
        ]
      ]
    ]

let resizeEvents initial =
  let handler dispatch = (fun _ -> dispatch (getCanvasSize()))
  let sub dispatch =
    window.addEventListener("resize", handler dispatch)
    window.addEventListener("load", handler dispatch)
  Cmd.ofSub sub


let run () =
  Program.mkProgram init update view
  |> Program.withSubscription resizeEvents
  |> Program.withDebugger
  |> Program.withReactBatched "elmish-app"
  |> Program.withConsoleTrace
  |> Program.run

promise {
  let! ctx_new = newFutharkContext()
  ctx <- ctx_new
  run()
} |> ignore

