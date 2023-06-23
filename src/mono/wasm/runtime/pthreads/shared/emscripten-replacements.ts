// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import MonoWasmThreads from "consts:monoWasmThreads";
import { afterLoadWasmModuleToWorker } from "../browser";
import { afterThreadInitTLS } from "../worker";
import Internals from "./emscripten-internals";
import { loaderHelpers } from "../../globals";
import { PThreadReplacements } from "../../types/internal";
import { mono_log_debug } from "../../logging";

/* a tag for the worker pool workers that are allocated here.  Mostly for debugging */
const monoWorkerSymbol = Symbol("MonoWorker");

function getMonoWorkerId(worker: Worker): string {
    return (<any>worker)?.[monoWorkerSymbol] ?? "(not mono!)";
}

/** @module emscripten-replacements Replacements for individual functions in the emscripten PThreads library.
 * These have a hard dependency on the version of Emscripten that we are using and may need to be kept in sync with
 *    {@linkcode file://./../../../emsdk/upstream/emscripten/src/library_pthread.js}
 */
export function replaceEmscriptenPThreadLibrary(replacements: PThreadReplacements): void {
    if (MonoWasmThreads) {
        const originalLoadWasmModuleToWorker = replacements.loadWasmModuleToWorker;
        replacements.loadWasmModuleToWorker = (worker: Worker): Promise<Worker> => {
            const p = originalLoadWasmModuleToWorker(worker);
            const monoId = getMonoWorkerId(worker);
            mono_log_debug (`worker ${monoId} loading runtime`);
            afterLoadWasmModuleToWorker(worker);
            return p;
        };
        const originalThreadInitTLS = replacements.threadInitTLS;
        replacements.threadInitTLS = (): void => {
            originalThreadInitTLS();
            afterThreadInitTLS();
        };
        // const originalAllocateUnusedWorker = replacements.allocateUnusedWorker;
        replacements.allocateUnusedWorker = replacementAllocateUnusedWorker;
        const originalReturnWorkerToPool = replacements.returnWorkerToPool;
        replacements.returnWorkerToPool = (worker: Worker): void => {
            const tid = (<any>worker)?.pthread_ptr?.toString(16);
            const monoId = getMonoWorkerId(worker);
            mono_log_debug(`returning unused worker ${monoId} to pool, was running 0x${tid}`);
            originalReturnWorkerToPool(worker);
        };
    }
}

let allocatedWorkerCount = 0;

/// We replace Module["PThreads"].allocateUnusedWorker with this version that knows about assets
function replacementAllocateUnusedWorker(): void {
    mono_log_debug("replacementAllocateUnusedWorker");
    const asset = loaderHelpers.resolve_asset_path("js-module-threads");
    const uri = asset.resolvedUrl;
    mono_assert(uri !== undefined, "could not resolve the uri for the js-module-threads asset");
    allocatedWorkerCount++;
    const worker = new Worker(uri);
    (<any>worker)[monoWorkerSymbol] = allocatedWorkerCount;
    Internals.getUnusedWorkerPool().push(worker);
}
