// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet, exit } from './dotnet.js'

let pumpOnce = null;

function displayMeaning(meaning) {
    document.getElementById("out").innerHTML = `${meaning}`;
}

let pumping = false;
let pumpCount = 0;

function requestPumping() {
    if (pumping !== undefined) {
        pumping = true;
        pumpCount = 0;
        setTimeout(doPump, 0);
    }
}

function doPump () {
    pumpCount++;
    if (pumpOnce (pumpCount)) {
        setTimeout(doPump, 0);
    }
}

try {
    const { setModuleImports, getConfig, getAssemblyExports } = await dotnet
        .withElementOnExit()
        .create();

    setModuleImports("main.js", {
        Sample: {
            Test: {
                displayMeaning
            },
            Scheduler: {
                requestPumping
            }
        }
    });
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);

    const demo = exports.Sample.Test.Demo;
    const demoSync = exports.Sample.Test.DemoSync;
    pumpOnce = exports.Sample.Scheduler.PumpOnce;

    document.getElementById("btnRunSync").addEventListener("click", () => {
        demoSync();
    });

    document.getElementById("btnRunGreen").addEventListener("click", async () => {
        await demo();
    });

    await dotnet.run();
}
catch (err) {
    exit(2, err);
}
