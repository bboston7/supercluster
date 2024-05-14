// Copyright 2024 Stellar Development Foundation and contributors. Licensed
// under the Apache License, Version 2.0. See the COPYING file at the root
// of this distribution or at http://www.apache.org/licenses/LICENSE-2.0

module MaxTPSTest

// This module provides a configurable max TPS test to use in missions

open Logging
open StellarCoreHTTP
open StellarCorePeer
open StellarCoreSet
open StellarFormation
open StellarMissionContext
open StellarNetworkData
open StellarStatefulSets
open StellarSupercluster

// Get the maximum value from a distribution
let private maxDistributionValue (distribution: (int * int) list) =
    fst (List.maxBy fst distribution)

// Upgrade max tx size to 2x the maximum possible from the distributions in
// `context`
let private upgradeSorobanTxLimits (context: MissionContext) (formation: StellarFormation) (coreSetList: CoreSet list)  =
    formation.SetupUpgradeContract coreSetList.Head

    let multiplier = 2
    let instructions = int64 ((maxDistributionValue context.instructionsDistribution) * multiplier)
    let txBytes = (maxDistributionValue context.totalKiloBytesDistribution) * multiplier * 1024
    let entries = (maxDistributionValue context.dataEntriesDistribution) * multiplier

    formation.DeployUpgradeEntriesAndArm
        coreSetList
        { LoadGen.GetDefault() with
              mode = CreateSorobanUpgrade
              txMaxInstructions = Some instructions
              txMaxReadBytes = Some txBytes
              txMaxWriteBytes = Some txBytes
              txMaxReadLedgerEntries = Some entries
              txMaxWriteLedgerEntries = Some entries
              // TODO: MIGHT need to adjust ledger tx size per block limit to
              // take into account this theoretical max tx size
              // NOTE: `txMaxSizeBytes` must be at least 10,000 bytes or
              // stellar-core will reject the upgrade
              txMaxSizeBytes = Some (max (maxDistributionValue context.txSizeBytesDistribution * multiplier) 10000)
              maxContractSizeBytes = Some (maxDistributionValue context.wasmBytesDistribution * multiplier)
              // TODO: What about the rest? (txMemoryLimit, maxContract*)
        }
        (System.DateTime.UtcNow)

    // TODO: Re-enable vv
    let peer = formation.NetworkCfg.GetPeer coreSetList.Head 0
    peer.WaitForTxMaxInstructions instructions |> ignore

let private upgradeSorobanLedgerLimits (context: MissionContext) (formation: StellarFormation) (coreSetList: CoreSet list) (txrate : int) =
    formation.SetupUpgradeContract coreSetList.Head

    let multiplier = txrate * 5 * 2
    let instructions = int64 ((maxDistributionValue context.instructionsDistribution) * multiplier)
    let maxBytes = (maxDistributionValue context.totalKiloBytesDistribution) * multiplier * 1024
    let entries = (maxDistributionValue context.dataEntriesDistribution) * multiplier

    formation.DeployUpgradeEntriesAndArm
        coreSetList
        { LoadGen.GetDefault() with
                mode = CreateSorobanUpgrade
                ledgerMaxInstructions = Some instructions
                ledgerMaxReadBytes = Some maxBytes
                ledgerMaxWriteBytes = Some maxBytes
                ledgerMaxTxCount = Some multiplier
                ledgerMaxReadLedgerEntries = Some entries
                ledgerMaxWriteLedgerEntries = Some entries
                ledgerMaxTransactionsSizeBytes = Some (maxDistributionValue context.txSizeBytesDistribution * multiplier)
        }
        (System.DateTime.UtcNow)

    let peer = formation.NetworkCfg.GetPeer coreSetList.Head 0
    peer.WaitForLedgerMaxInstructions instructions |> ignore


let maxTPSTest
    (context: MissionContext)
    (baseLoadGen: LoadGen)
    (setupCfg: LoadGen option)
    (increaseSorobanLimits: bool)
    =
    let allNodes =
        if context.pubnetData.IsSome then
            FullPubnetCoreSets context true false
        else
            StableApproximateTier1CoreSets
                context.image
                (if context.flatQuorum.IsSome then context.flatQuorum.Value else false)

    let sdf =
        List.find (fun (cs: CoreSet) -> cs.name.StringName = "stellar" || cs.name.StringName = "sdf") allNodes

    let tier1 = List.filter (fun (cs: CoreSet) -> cs.options.tier1 = Some true) allNodes

    context.ExecuteWithOptionalConsistencyCheck
        allNodes
        None
        false
        (fun (formation: StellarFormation) ->

            let numAccounts = 30000

            let upgradeMaxTxSetSize (coreSets: CoreSet list) (rate: int) =
                // Set max tx size to 10x the rate -- at 5x we overflow the transaction queue too often.
                formation.UpgradeMaxTxSetSize coreSets (10 * rate)

            // Setup overlay connections first before manually closing
            // ledger, which kick off consensus
            formation.WaitUntilConnected allNodes
            formation.ManualClose allNodes

            // Wait until the whole network is synced before proceeding,
            // to fail asap in case of a misconfiguration
            formation.WaitUntilSynced allNodes
            formation.UpgradeProtocolToLatest allNodes
            upgradeMaxTxSetSize allNodes 10000
            formation.RunLoadgen sdf { context.GenerateAccountCreationLoad with accounts = numAccounts }

            // // Upgrade limits (if requested)
            // if increaseSorobanLimits then
            //     // TODO: Remove
            //     // formation.UpgradeSorobanLedgerLimitsWithMultiplier allNodes 100000
            //     // formation.UpgradeSorobanTxLimitsWithMultiplier allNodes 1000
            //     upgradeSorobanTxLimits context formation allNodes
            //     failwith "success?"

            // TODO: Docs
            let mutable upgradedTxLimits = false

            // Perform setup (if requested)
            match setupCfg with
            | Some cfg -> formation.RunLoadgen sdf cfg
            | None -> ()

            let wait () = System.Threading.Thread.Sleep(30 * 1000)

            let getMiddle (low: int) (high: int) = low + (high - low) / 2

            let binarySearchWithThreshold (low: int) (high: int) (threshold: int) =

                let mutable lowerBound = low
                let mutable upperBound = high
                let mutable shouldWait = false
                let mutable finalTxRate = None

                while upperBound - lowerBound > threshold do
                    let middle = getMiddle lowerBound upperBound

                    if shouldWait then wait ()

                    formation.clearMetrics allNodes
                    upgradeMaxTxSetSize allNodes middle

                    if increaseSorobanLimits then
                        upgradeSorobanLedgerLimits context formation allNodes middle
                        if not upgradedTxLimits then
                            upgradedTxLimits <- true
                            upgradeSorobanTxLimits context formation allNodes

                    try
                        LogInfo "Run started at tx rate %i" middle

                        let loadGen =
                            { baseLoadGen with
                                  accounts = numAccounts
                                  // TODO: Remove the `/5`. Also double check
                                  // that all values have been properly raised
                                  // back up. ALSO check in the `max-tps-mixed`
                                  // branch
                                  txs = middle * 1000 / 5
                                  txrate = middle }

                        formation.RunMultiLoadgen tier1 loadGen
                        formation.CheckNoErrorsAndPairwiseConsistency()
                        formation.EnsureAllNodesInSync allNodes

                        // Increase the tx rate
                        lowerBound <- middle
                        finalTxRate <- Some middle
                        LogInfo "Run succeeded at tx rate %i" middle
                        shouldWait <- false

                    with _ ->
                        LogInfo "Run failed at tx rate %i" middle
                        upperBound <- middle
                        shouldWait <- true

                if finalTxRate.IsSome then
                    LogInfo "Found max tx rate %i" finalTxRate.Value

                    if context.maxTxRate - finalTxRate.Value <= threshold then
                        LogInfo "Tx rate reached the upper bound, increase max-tx-rate for more accurate results"
                else
                    failwith (sprintf "No successful runs at tx rate %i or higher" lowerBound)

                finalTxRate.Value

            let mutable results = []
            // As the runs take a while, set a threshold of 10, so we get a
            // reasonable approximation
            let threshold = 10
            let numRuns = if context.numRuns.IsSome then context.numRuns.Value else 1

            for run in 1 .. numRuns do
                LogInfo "Starting max TPS run %i out of %i" run numRuns
                let resultRate = binarySearchWithThreshold context.txRate context.maxTxRate threshold
                results <- List.append results [ resultRate ]
                if run < numRuns then wait ()

            LogInfo
                "Final tx rate averaged to %i over %i runs for image %s"
                (results |> List.map float |> List.average |> int)
                numRuns
                context.image)
