#! /usr/bin/env fsharpi

let (=>) left right = Printf.printf (if left = right then "." else "F")
let format = Printf.sprintf

type term = Con of int | Div of term * term

let rec show_term = function
  | Con a -> format "Con %i" a
  | Div (t, u) -> format "Div (%s, %s)" (show_term t) (show_term u)

module TestNoMonad =
  let rec eval = function
    | Con a -> a
    | Div (t, u) -> eval t / eval u

  let test_eval =
    eval (Div (Div (Con 1972, Con 2), Con 23)) => 42;
    try ignore (eval (Div (Con 1, Con 0)))
    with :? System.DivideByZeroException -> ()

module IdentityMonad =
  type 'a t = 'a

  let return' t = t
  let (>>=) a k = k a

module TestIdentityMonad =
  open IdentityMonad

  let test_identity_monad =
    begin
      1 >>= fun a ->
      2 >>= fun b ->
      return' (a + b)
    end => 3

  let rec eval = function
    | Con a -> return' a
    | Div (t, u) ->
        eval t >>= fun a ->
        eval u >>= fun b ->
        return' (a / b)

  let test_eval =
    eval (Div (Div (Con 1972, Con 2), Con 23)) => 42;
    try ignore (eval (Div (Con 1, Con 0)))
    with :? System.DivideByZeroException -> ()

module ExceptionMonad =
  type exception_ = string
  type 'a t = Raise of exception_ | Return of 'a

  let return' t = Return t
  let (>>=) m k =
    match m with
    | Raise e -> Raise e
    | Return a -> k a

  let raise_ e = Raise e

module TestExceptionMonad =
  open ExceptionMonad

  let test_exception_monad =
    begin
      return' 1 >>= fun a ->
      return' 0 >>= fun b ->
      if b = 0
        then raise_ "divide by zero"
        else return' (a / b)
    end => Raise "divide by zero"

  let rec eval = function
    | Con a -> return' a
    | Div (t, u) ->
        eval t >>= fun a ->
        eval u >>= fun b ->
        if b = 0
          then raise_ "divide by zero"
          else return' (a / b)

  let test_eval =
    eval (Div (Div (Con 1972, Con 2), Con 23)) => Return 42;
    eval (Div (Con 1, Con 0)) => Raise "divide by zero"

module StateMonad =
  type state = int
  type 'a t = state -> 'a * state

  let return' a = fun x -> (a, x)
  let (>>=) m k =
    fun x ->
      let a, y = m x in
      let b, z = k a y in
      b, z

  let tick = fun x -> (), x + 1

module TestStateMonad =
  open StateMonad

  let test_state_monad =
    begin
      return' 11 >>= fun a ->
      tick       >>= fun () ->
      return' 22 >>= fun b ->
      tick       >>= fun () ->
      return' (a + b)
    end 0 => (33, 2)

  let rec eval = function
    | Con a -> return' a
    | Div (t, u) ->
        eval t >>= fun a ->
        eval u >>= fun b ->
        tick   >>= fun () ->
        return' (a / b)

  let test_eval =
    eval (Div (Div (Con 1972, Con 2), Con 23)) 0 => (42, 2)

module OutputMonad =
  type output = string
  type 'a t = output * 'a

  let return' a = "", a
  let (>>=) m k =
    let x, a = m in
    let y, b = k a in
    x + y, b

  let out x = x, ()

module TestOutputMonad =
  open OutputMonad

  let test_output_monad =
    begin
      return' 11 >>= fun a ->
      out "hai"  >>= fun () ->
      return' 22 >>= fun b ->
      out "bye"  >>= fun () ->
      return' (a + b)
    end => ("haibye", 33)

  let line term value =
    format "eval (%s) ⇐ %i \n" (show_term term) value

  let rec eval = function
    | Con a as term ->
        out (line term a) >>= fun () ->
        return' a
    | Div (t, u) as term ->
        eval t >>= fun a ->
        eval u >>= fun b ->
        out (line term (a / b)) >>= fun () ->
        return' (a / b)

  let test_eval =
    let output = "eval (Con 1972) ⇐ 1972 \n" +
                 "eval (Con 2) ⇐ 2 \n" +
                 "eval (Div (Con 1972, Con 2)) ⇐ 986 \n" +
                 "eval (Con 23) ⇐ 23 \n" +
                 "eval (Div (Div (Con 1972, Con 2), Con 23)) ⇐ 42 \n" in
    eval (Div (Div (Con 1972, Con 2), Con 23)) => (output, 42);
