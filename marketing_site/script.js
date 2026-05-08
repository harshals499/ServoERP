const DOWNLOAD_URL =
  "https://downloads.servoerp.in/ServoERP.Setup.1.0.20.0.exe";
const SALES_EMAIL = "harshalsonawane@servoerp.com";

const header = document.querySelector(".site-header");
const navToggle = document.querySelector(".nav-toggle");
const downloadLinks = document.querySelectorAll("[data-download-link]");
const leadForm = document.querySelector("[data-lead-form]");

downloadLinks.forEach((link) => {
  link.href = DOWNLOAD_URL;
  link.rel = "noopener";
});

navToggle?.addEventListener("click", () => {
  const expanded = navToggle.getAttribute("aria-expanded") === "true";
  navToggle.setAttribute("aria-expanded", String(!expanded));
  header.classList.toggle("nav-open", !expanded);
});

document.querySelectorAll(".site-nav a").forEach((link) => {
  link.addEventListener("click", () => {
    navToggle?.setAttribute("aria-expanded", "false");
    header?.classList.remove("nav-open");
  });
});

leadForm?.addEventListener("submit", (event) => {
  event.preventDefault();
  const data = new FormData(leadForm);
  const subject = encodeURIComponent("ServoERP demo/license request");
  const body = encodeURIComponent(
    [
      "ServoERP enquiry",
      "",
      "Company: " + (data.get("company") || ""),
      "Contact person: " + (data.get("name") || ""),
      "Phone: " + (data.get("phone") || ""),
      "Email: " + (data.get("email") || ""),
      "Industry: " + (data.get("industry") || ""),
      "Users/devices: " + (data.get("users") || ""),
      "Plan interest: " + (data.get("plan") || ""),
      "",
      "Message:",
      data.get("message") || "",
    ].join("\n")
  );
  window.location.href = `mailto:${SALES_EMAIL}?subject=${subject}&body=${body}`;
});
