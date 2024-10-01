// Copyright 2024 Stellar Development Foundation and contributors. Licensed
// under the Apache License, Version 2.0. See the COPYING file at the root
// of this distribution or at http://www.apache.org/licenses/LICENSE-2.0

module MissionTweakableOrgs

// TODO: Update docs below
// The point of this mission is to simulate pubnet as closely as possible,
// for evaluating the likely effect of a change to core when deployed.

open MaxTPSTest
open PubnetData
open StellarCoreSet
open StellarMissionContext
open StellarFormation
open StellarStatefulSets
open StellarNetworkData
open StellarSupercluster
open StellarCoreHTTP

// TODO: Dedup with function in MaxTPSMixed
// If `list` is empty, return `value`. Otherwise, return `list`.
let private defaultListValue value list =
    match list with
    | [] -> value
    | _ -> list

let tweakableOrgs(baseContext: MissionContext) =
    let context =
        { baseContext with
              numAccounts = 30000
              coreResources = TweakableOrgsResources
              // When no value is given, use the default values derived from observing the pubnet.
              simulateApplyDuration = Some(baseContext.simulateApplyDuration |> Option.defaultValue pubnetApplyDuration)
              simulateApplyWeight = Some(baseContext.simulateApplyWeight |> Option.defaultValue pubnetApplyWeight)
              // As the goal of `SimulatePubnet` is to simulate a pubnet,
              // network delays are, in general, indispensable.
              // Therefore, unless explicitly told otherwise, we will use
              // network delays.
              installNetworkDelay = Some(baseContext.installNetworkDelay |> Option.defaultValue true)
              enableTailLogging = false
              wasmBytesDistribution = defaultListValue pubnetWasmBytes baseContext.wasmBytesDistribution
              dataEntriesDistribution = defaultListValue pubnetDataEntries baseContext.dataEntriesDistribution
              totalKiloBytesDistribution = defaultListValue pubnetTotalKiloBytes baseContext.totalKiloBytesDistribution

              txSizeBytesDistribution = defaultListValue pubnetTxSizeBytes baseContext.txSizeBytesDistribution
              instructionsDistribution = defaultListValue pubnetInstructions baseContext.instructionsDistribution }

    let fullCoreSet = FullPubnetCoreSets context true false

    let tier1 = List.filter (fun (cs: CoreSet) -> cs.options.tier1 = Some true) fullCoreSet

    let nonTier1 = List.filter (fun (cs: CoreSet) -> cs.options.tier1 <> Some true) fullCoreSet

    // Transactions per second. ~1000 per ledger. 200 payment TPS and 2 invoke
    // TPS.
    let txrate = 202

    let loadGen =
        { LoadGen.GetDefault() with
              mode = MixedClassicSoroban
              // TODO: Should I even let context override spikesize and
              // spikeinterval?
              spikesize = context.spikeSize
              spikeinterval = context.spikeInterval
              offset = 0
              maxfeerate = None
              skiplowfeetxs = false
              accounts = context.numAccounts

              wasms = context.numWasms
              instances = context.numInstances

              // ~1000 transactions per ledger
              txrate = txrate

              // ~15 minutes of load
              txs = txrate * 60 * 15

              // Blend settings. 99% classic, 1% invoke by default
              payWeight = Some(baseContext.payWeight |> Option.defaultValue pubnetPayWeight)
              sorobanInvokeWeight = Some(baseContext.sorobanInvokeWeight |> Option.defaultValue pubnetInvokeWeight)
              sorobanUploadWeight = Some(baseContext.sorobanUploadWeight |> Option.defaultValue pubnetUploadWeight)

              // Require a majority of Soroban transactions to succeed.
              minSorobanPercentSuccess = Some(baseContext.minSorobanPercentSuccess |> Option.defaultValue 60) }

    context.Execute
        fullCoreSet
        None
        (fun (formation: StellarFormation) ->
            // Setup overlay connections first before manually closing
            // ledger, which kick off consensus
            formation.WaitUntilConnected fullCoreSet
            formation.ManualClose tier1

            // Wait until the whole network is synced before proceeding,
            // to fail asap in case of a misconfiguration
            formation.WaitUntilSynced fullCoreSet

            // Upgrades!
            formation.UpgradeProtocolToLatest tier1
            formation.UpgradeMaxTxSetSize tier1 (txrate * 10)
            upgradeSorobanLedgerLimits context formation fullCoreSet txrate
            upgradeSorobanTxLimits context formation fullCoreSet

            formation.RunLoadgen nonTier1.Head context.GenerateAccountCreationLoad

            // TODO: Multi-loadgen?
            formation.RunLoadgen nonTier1.Head loadGen
            formation.CheckNoErrorsAndPairwiseConsistency()
            formation.EnsureAllNodesInSync fullCoreSet)
