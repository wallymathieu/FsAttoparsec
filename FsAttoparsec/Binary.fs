﻿namespace Attoparsec

(*
Copyright 2010-2013

    Steffen Forkmann (http://navision-blog.de/)
    Tomas Petricek (http://tomasp.net/)
    Ryan Riley (http://panesofglass.org/)
    Mauricio Scheffer (http://bugsquash.blogspot.com/)
    Jack Fox (http://jackfoxy.com/)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*)

// * Original Implementation
//     * FSharpx.Collections.ByteString
// * Modification
//     * rename class name
//     * modify namespace
//     * remove ofString and toString
//     * fix splitAt and empty functions
//     * remove document comments(TODO: add new document)
//     * add range function

open System
open System.Collections
open System.Collections.Generic

[<CustomEquality; CustomComparison; Struct>]
#if ! NETSTANDARD1_6
[<Serializable>]
#endif
type BinaryArray(array: byte [], offset: int, count: int) =
  new (array: byte []) = BinaryArray(array, 0, array.Length)

  member x.Array = array
  member x.Offset = offset
  member x.Count = count

  static member Compare (a: BinaryArray, b: BinaryArray) =
    let x, o, l = a.Array, a.Offset, a.Count
    let x', o', l' = b.Array, b.Offset, b.Count
    if o = o' && l = l' && x = x' then 0
    elif x = x' then
      if o = o' then if l < l' then -1 else 1
      else if o < o' then -1 else 1
    else
      let left, right = x.[o .. (o + l - 1)], x'.[o' .. (o' + l' - 1)]
      if left = right then 0 elif left < right then -1 else 1

  override x.Equals(other) =
    match other with
    | :? BinaryArray as other' -> BinaryArray.Compare(x, other') = 0
    | _ -> false

  override x.GetHashCode() = hash (x.Array,x.Offset,x.Count)

  member x.GetEnumerator() =
    if x.Count = 0 then
      { new IEnumerator<_> with
          member self.Current = invalidOp "!"
        interface System.Collections.IEnumerator with
          member self.Current = invalidOp "!"
          member self.MoveNext() = false
          member self.Reset() = ()
        interface System.IDisposable with
          member self.Dispose() = () }
    else
      let segment = x.Array
      let minIndex = x.Offset
      let maxIndex = x.Offset + x.Count - 1
      let currentIndex = ref <| minIndex - 1
      { new IEnumerator<_> with
          member self.Current =
            if !currentIndex < minIndex then
              invalidOp "Enumeration has not started. Call MoveNext."
            elif !currentIndex > maxIndex then
              invalidOp "Enumeration already finished."
            else segment.[!currentIndex]
        interface System.Collections.IEnumerator with
          member self.Current =
            if !currentIndex < minIndex then
              invalidOp "Enumeration has not started. Call MoveNext."
            elif !currentIndex > maxIndex then
              invalidOp "Enumeration already finished."
            else box segment.[!currentIndex]
          member self.MoveNext() =
            if !currentIndex < maxIndex then
              incr currentIndex
              true
            else false
          member self.Reset() = currentIndex := minIndex - 1
        interface System.IDisposable with
          member self.Dispose() = () }

  interface System.IComparable with
    member x.CompareTo(other) =
      match other with
      | :? BinaryArray as other' -> BinaryArray.Compare(x, other')
      | _ -> invalidArg "other" "Cannot compare a value of another type."

  interface System.Collections.Generic.IEnumerable<byte> with
    member x.GetEnumerator() = x.GetEnumerator()
    member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BinaryArray =

  let empty = BinaryArray([||])

  let singleton c = BinaryArray(Array.create 1 c, 0, 1)

  let create arr = BinaryArray(arr, 0, arr.Length)

  let findIndex pred (bs:BinaryArray) =
    Array.FindIndex(bs.Array, bs.Offset, bs.Count, Predicate<_>(pred))

  let ofArray array = BinaryArray(array)

  let ofSeq s = let arr = Array.ofSeq s in BinaryArray(arr, 0, arr.Length)

  let ofList l = BinaryArray(Array.ofList l, 0, l.Length)

  let toArray (bs:BinaryArray) =
    if bs.Count = 0 then [||]
    else bs.Array.[ bs.Offset .. (bs.Offset + bs.Count - 1) ]

  let toSeq (bs:BinaryArray) = bs :> seq<byte>

  let toList (bs:BinaryArray) = List.ofSeq bs

  let isEmpty (bs:BinaryArray) =
    Contract.Requires(bs.Count >= 0)
    bs.Count <= 0

  let length (bs:BinaryArray) =
    Contract.Requires(bs.Count >= 0)
    bs.Count

  let index (bs:BinaryArray) pos =
    Contract.Requires(bs.Offset + pos <= bs.Count)
    bs.Array.[ bs.Offset + pos ]

  let head (bs:BinaryArray) =
    if bs.Count <= 0 then
      failwith "Cannot take the head of an empty Binary."
    else bs.Array.[ bs.Offset ]

  let tail (bs:BinaryArray) =
    Contract.Requires(bs.Count >= 1)
    if bs.Count = 1 then empty
    else BinaryArray(bs.Array, bs.Offset + 1, bs.Count - 1)

  let append a b =
    if isEmpty a then b
    elif isEmpty b then a
    else
      let x, o, l = a.Array, a.Offset, a.Count
      let x', o', l' = b.Array, b.Offset, b.Count
      let buffer = Array.zeroCreate<byte> (l + l')
      Buffer.BlockCopy(x, o, buffer, 0, l)
      Buffer.BlockCopy(x', o', buffer, l, l')
      BinaryArray(buffer, 0, l+l')

  let cons hd (bs:BinaryArray) =
    let hd = singleton hd
    if length bs = 0 then hd
    else append hd bs

  let fold f seed bs =
    let rec loop bs acc =
      if isEmpty bs then acc
      else
        let hd, tl = head bs, tail bs
        loop tl (f acc hd)
    loop bs seed

  let split pred (bs:BinaryArray) =
    if isEmpty bs then empty, empty
    else
      let index = findIndex pred bs
      if index = -1 then bs, empty
      else
        let count = index - bs.Offset
        BinaryArray(bs.Array, bs.Offset, count),
        BinaryArray(bs.Array, index, bs.Count - count)

  let span pred bs = split (not << pred) bs

  let splitAt n (bs:BinaryArray) =
    Contract.Requires(n >= 0)
    if isEmpty bs then empty, empty
    elif n <= 0 then empty, bs
    elif n >= bs.Count then bs, empty
    else
      let x,o,l = bs.Array, bs.Offset, bs.Count
      BinaryArray(x, o, n), BinaryArray(x, o + n, l - n)

  let skip n bs = splitAt n bs |> snd

  let skipWhile pred bs = span pred bs |> snd

  let skipUntil pred bs = split pred bs |> snd

  let take n bs = splitAt n bs |> fst

  let takeWhile pred bs = span pred bs |> fst

  let takeUntil pred bs = split pred bs |> fst

  let range pos n ba = take n (skip pos ba)

  let monoid = { new Monoid<_> with
    override x.Mempty = empty
    override x.Mappend(a, b) = append a b }
