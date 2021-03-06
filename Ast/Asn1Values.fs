﻿module Asn1Values

open System.Numerics
open FsUtils
open Ast
open uPER






let rec GetDefaultValueByType (t:Asn1Type) (m:Asn1Module) (r:AstRoot) = 
    let getVal vKind = { Asn1Value.Kind = vKind; Location = emptyLocation}
    match t.Kind with
    | Integer   ->
        match (GetTypeUperRange t.Kind t.Constraints r) with
        |Concrete(a, _)         
        |NegInf(a)              
        |PosInf(a)             -> getVal (IntegerValue (loc a))
        |Full                   
        |Empty                 -> getVal (IntegerValue (loc 0I))
    | Real     ->                 getVal (RealValue (loc 0.0))
    | IA5String | NumericString ->
        let chr = GetTypeUperRangeFrom(t.Kind,t.Constraints, r) |> Seq.filter(fun c -> not (System.Char.IsControl c)) |> Seq.head
        let min,max = uPER.GetSizebaleMinMax t.Kind t.Constraints r
        getVal (StringValue (loc (System.String(chr,int min)) ))
    | OctetString               ->
        let min,max = uPER.GetSizebaleMinMax t.Kind t.Constraints r
        let chVals = [1I..min] |> List.map(fun i -> ByteLoc.ByValue 0uy)
        getVal (OctetStringValue chVals)
    | BitString                 ->
        let min,max = uPER.GetSizebaleMinMax t.Kind t.Constraints r
        getVal (BitStringValue (loc (System.String('0',int min)) ))
    | Boolean                   ->getVal (BooleanValue (loc false))
    | Enumerated(items)         ->getVal (RefValue (m.Name, items.Head.Name))
    | SequenceOf(child)         ->
        let min,max = uPER.GetSizebaleMinMax t.Kind t.Constraints r
        let chVals = [1I..min] |> List.map(fun i -> GetDefaultValueByType child m r )
        getVal (SeqOfValue chVals)
    | Choice(children)          ->
        let fc = children.Head
        getVal (ChValue (fc.Name, GetDefaultValueByType fc.Type m r))
    | Sequence(children)        ->
        let getChildValue (c:ChildInfo) =
            match c.Optionality with
            |Some(Default(v))   -> c.Name, v
            | _                 -> c.Name, (GetDefaultValueByType c.Type m r)
        let chVals = children |> List.filter(fun x-> not x.AcnInsertedField) |> List.map getChildValue
        getVal (SeqValue chVals)
    | NullType                  -> getVal NullValue
    |ReferenceType(modName,tasName, _) ->
        let newModule = r.GetModuleByName modName
        let baseType = Ast.GetActualTypeAllConsIncluded t r
        GetDefaultValueByType baseType newModule r