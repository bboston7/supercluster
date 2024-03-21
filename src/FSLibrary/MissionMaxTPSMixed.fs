// Copyright 2024 Stellar Development Foundation and contributors. Licensed
// under the Apache License, Version 2.0. See the COPYING file at the root
// of this distribution or at http://www.apache.org/licenses/LICENSE-2.0

module MissionMaxTPSMixed

// TODO: Docs

open MaxTPSTest
open StellarMissionContext
open StellarCoreHTTP

let maxTPSMixed (baseContext: MissionContext) =
    let context =
        { baseContext with
              coreResources = SimulatePubnetTier1PerfResources
              installNetworkDelay = Some(baseContext.installNetworkDelay |> Option.defaultValue true)
              // No additional DB overhead unless specified (this will measure the in-memory SQLite DB only)
              simulateApplyDuration = Some(baseContext.simulateApplyDuration |> Option.defaultValue (seq { 0 }))
              simulateApplyWeight = Some(baseContext.simulateApplyWeight |> Option.defaultValue (seq { 100 }))
              enableTailLogging = false }

    let baseLoadGen =
        { LoadGen.GetDefault() with
                mode = MixedClassicSoroban
                spikesize = context.spikeSize
                spikeinterval = context.spikeInterval
                offset = 0
                maxfeerate = None
                skiplowfeetxs = false

                // NOTE: Data set from https://docs.google.com/spreadsheets/d/1l92InLSZ_2SUFJnNIipfJCehYedI1jL-vKBHZ5NYjUM/edit#gid=879538951

                // SOROBAN_UPLOAD settings (set from testnet data)
                wasmBytesIntervals = [for x in 0..4 -> x * 16 * 1024]
                wasmBytesWeights = [132 ; 68 ; 92 ; 141]

                // TODO: Override wasms and instances if None?
                wasms = context.numWasms
                instances = context.numInstances
                dataEntriesIntervals = [0 ; 6; 12; 18; 24]
                dataEntriesWeights = [380 ; 42 ; 5 ; 2]
                // TODO: This isn't very granular, and  the average write size
                // is actually under 1KB. I rounded anything under 1KB up, and
                // anything over 1.5KB to 2KB. This skews the distribution a bit
                // rightwards.
                // TODO: I think this is wrong. It's bytes written TOTAL, not
                // per entry. I set the distributions as if it were per entry
                totalBytesIntervals = [0 ; 1500 ; 3000 ; 4500 ; 6000]
                totalBytesWeights = [382 ; 36 ; 10 ; 2]
                txSizeBytesIntervals = [100; 300; 500; 700; 900; 1100]
                txSizeBytesWeights = [37; 6; 1; 4; 1]
                instructionsIntervals =
                    [0L ; 25000000L ; 50000000L ; 75000000L ; 100000000L]
                instructionsWeights = [201 ; 183 ; 34 ; 11]

                // Blend settings
                payWeight = Some 50
                sorobanUploadWeight = Some 5
                sorobanInvokeWeight = Some 45

                // TODO: Docs
                minSorobanPercentSuccess = Some 80
        }

    // TODO Need to pass a function to maxTPSTest that sets up the blended mode
    maxTPSTest context baseLoadGen (Some context.SetupSorobanInvoke)