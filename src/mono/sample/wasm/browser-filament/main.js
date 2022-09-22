// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet, exit } from './dotnet.js'

function updateTick(text) {
    document.getElementById('tick').innerHTML = text;
}

let pumpOnce = null;

function displayMessage(meaning) {
    document.getElementById("out").innerHTML = `${meaning}`;
}

let pumping = false;
let pumpCount = 0;

function requestPumping() {
    function doPump() {
        pumpCount++;
        if (pumpOnce(pumpCount)) {
            setTimeout(doPump, 0);
        } else {
            pumping = false;
            console.log(`finished pumping after ${pumpCount} iterations`);
        }
    }
    if (pumping === false) {
        console.log("starting pumping");
        pumping = true;
        pumpCount = 0;
        setTimeout(doPump, 0);
    }
}

try {
    const { setModuleImports, getConfig, getAssemblyExports } = await dotnet
        //.withElementOnExit()
        .create();

    setModuleImports("main.js", {
        Sample: {
            Test: {
                displayMessage,
                updateTick,
            },
        },
        Filaments: {
            Scheduler: {
                requestPumping
            }
        }
    });
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);

    const demo = exports.Sample.Test.Demo;
    const demoSync = exports.Sample.Test.DemoSync;
    pumpOnce = exports.Filaments.Scheduler.PumpOnce;

    document.getElementById("btnRunSync").addEventListener("click", () => {
        demoSync();
    });

    document.getElementById("btnRunGreen").addEventListener("click", async () => {
        await demo();
    });

    exports.Sample.Test.Tick();

    await dotnet.run();
}
catch (err) {
    exit(2, err);
}
