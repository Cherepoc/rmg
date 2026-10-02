/**
 *     Work under way, shown on what waits for it: a bar along its bottom that fills as the work goes where how far it
 *     has gone is known, and sweeps where it is not, read out as a progress bar of what it is; gone once it is done.
 */
export function showWork(host, label, fraction = null) {
    let bar = host.querySelector(":scope > .work");
    if (bar === null) {
        bar = document.createElement("span");
        bar.className = "work";
        bar.setAttribute("role", "progressbar");
        bar.append(document.createElement("span"));
        host.classList.add("working");
        host.append(bar);
    }

    bar.setAttribute("aria-label", label);
    bar.classList.toggle("sweeping", fraction === null);
    if (fraction === null) bar.removeAttribute("aria-valuenow");
    else bar.setAttribute("aria-valuenow", String(Math.round(fraction * 100)));
    bar.style.setProperty("--done", String(fraction ?? 0));
}

export function hideWork(host) {
    host.querySelector(":scope > .work")?.remove();
    host.classList.remove("working");
}
