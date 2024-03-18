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
                mode = GeneratePaymentLoad
                spikesize = context.spikeSize
                spikeinterval = context.spikeInterval
                offset = 0
                maxfeerate = None
                skiplowfeetxs = false }

    // TODO Need to pass a function to maxTPSTest that sets up the blended mode
    maxTPSTest context baseLoadGen