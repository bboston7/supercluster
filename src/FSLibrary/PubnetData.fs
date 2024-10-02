// Copyright 2024 Stellar Development Foundation and contributors. Licensed
// under the Apache License, Version 2.0. See the COPYING file at the root
// of this distribution or at http://www.apache.org/licenses/LICENSE-2.0

module PubnetData

// TODO: Docs. Mention when these were updated and how. Mention they are all parts per thousand (at least, the loadgen ones are. Not the apply ones).

// TODO: Pull script into repo

let pubnetApplyDuration =
    seq {
        20
        38
        97
        425
        990
    }


let pubnetApplyWeight =
    seq {
        25
        50
        20
        4
        1
    }

// TODO: Trim some of these soroban distributions. Document how and why I
// trimmed them.
let pubnetInstructions = [(2000000, 1000)]
    // [ (2026523, 101)
    //   (5826121, 52)
    //   (9625719, 360)
    //   (13425317, 76)
    //   (17224915, 39)
    //   (21024513, 330)
    //   (24824111, 24)
    //   (28623709, 13)
    //   (32423307, 2)
    //   (36222905, 3) ]

let pubnetTotalKiloBytes = [ (2, 1000) ]
 //[ (0, 132); (1, 258); (2, 63); (3, 203); (4, 106); (5, 227); (6, 7); (7, 1) ] //

let pubnetTxSizeBytes =
    [ (484, 105)
      (876, 43)
      (1268, 76)
      (1660, 261)
      (2052, 255)
      (2444, 204)
      (2836, 33)
      (3228, 18)
      (3620, 2)
      (4012, 3) ]

let pubnetWasmBytes =
    [ (3333, 296)
      (9512, 202)
      (15691, 116)
      (21870, 163)
      (28049, 77)
      (34228, 94)
      (40407, 26)
      (46586, 13)
      (52765, 9)
      (58944, 4) ]

let pubnetDataEntries = [ (1, 667); (2, 333) ]
    // [ (1, 116)
    //   (3, 43)
    //   (4, 151)
    //   (6, 285)
    //   (8, 41)
    //   (10, 111)
    //   (12, 92)
    //   (13, 9)
    //   (15, 1)
    //   (17, 149) ]

let pubnetPayWeight = 99
let pubnetInvokeWeight = 1
let pubnetUploadWeight = 0
