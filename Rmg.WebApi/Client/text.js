/** Seconds as minutes and whole seconds, "2:49", none or less as "0:00"; a length to round rounded first. */
export function minutesAndSeconds(seconds) {
    const whole = Number.isFinite(seconds) && seconds > 0 ? Math.floor(seconds) : 0;
    return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

/** A page's status line saying something, marked as an error where it is one. */
export function showStatus(element, message, isError = false) {
    element.textContent = message;
    element.classList.toggle("error", isError);
}
