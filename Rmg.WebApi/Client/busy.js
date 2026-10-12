/**
 *     Work under way, shown on what waits for it: a bar along its bottom that fills as the work goes where how far it
 *     has gone is known, and sweeps where it is not, read out as a progress bar of what it is; gone once it is done. On a
 *     button, whose children a screen reader takes as its name, the bar is shown alone and the button read as busy.
 */
export function showWork(host, label, fraction = null) {
    let bar = host.querySelector(":scope > .work");
    if (bar === null) {
        bar = document.createElement("span");
        bar.className = "work";
        if (host instanceof HTMLButtonElement) {
            bar.setAttribute("aria-hidden", "true");
            host.setAttribute("aria-busy", "true");
        } else bar.setAttribute("role", "progressbar");
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
    host.removeAttribute("aria-busy");
}
