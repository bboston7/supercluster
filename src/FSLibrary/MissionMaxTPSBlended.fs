// Copyright 2024 Stellar Development Foundation and contributors. Licensed
// under the Apache License, Version 2.0. See the COPYING file at the root
// of this distribution or at http://www.apache.org/licenses/LICENSE-2.0

module MissionMaxTPSBlended

// TODO: Docs

open MaxTPSTest
open StellarMissionContext
open StellarCoreHTTP

let maxTPSBlended (baseContext: MissionContext) =
    let context =
        { baseContext with
              coreResources = SimulatePubnetTier1PerfResources
              installNetworkDelay = Some(baseContext.installNetworkDelay |> Option.defaultValue true)
              // Simulated apply delays result in disabled history modes blended
              // mode with non-zero invoke success rates requires. Therefore, we
              // must disable these options.
              simulateApplyDuration = None
              simulateApplyWeight = None
              enableTailLogging = false }

    let baseLoadGen =
        { LoadGen.GetDefault() with
                mode = BlendClassicSoroban
                spikesize = context.spikeSize
                spikeinterval = context.spikeInterval
                offset = 0
                maxfeerate = None
                skiplowfeetxs = false

                // SOROBAN_UPLOAD settings (set from testnet data)
                wasmBytesIntervals = [for x in 0..4 -> x * 16 * 1024]
                wasmBytesWeights = [91 ; 50 ; 92 ; 64]

                // TODO: SOROBAN_INVOKE settings

                // Blend settings
                payWeight = Some 50
                sorobanUploadWeight = Some 5
                sorobanInvokeWeight = Some 45
        }

    // TODO Need to pass a function to maxTPSTest that sets up the blended mode
    maxTPSTest context baseLoadGen