const DOWNLOAD_URL =
  "https://servoerp.in/download/";
const DEMO_REQUEST_ENDPOINT = "/api/demo-request";
const DEMO_SUCCESS_MESSAGE = "Thank you! Our team will contact you shortly.";
const DEMO_REQUEST_EMAIL = "support@servoerp.in";

const header = document.querySelector(".site-header");
const navToggle = document.querySelector(".nav-toggle");
const downloadLinks = document.querySelectorAll("[data-download-link]");
const leadForm = document.querySelector("[data-lead-form]");
const revealItems = document.querySelectorAll(".reveal");
const screenshotImages = document.querySelectorAll('img[src*="assets/screenshots"]');

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

if ("IntersectionObserver" in window) {
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        }
      });
    },
    { rootMargin: "0px 0px -12% 0px", threshold: 0.12 }
  );

  revealItems.forEach((item) => observer.observe(item));
} else {
  revealItems.forEach((item) => item.classList.add("is-visible"));
}

if (screenshotImages.length) {
  const lightbox = document.createElement("div");
  lightbox.className = "image-lightbox";
  lightbox.hidden = true;
  lightbox.innerHTML = `
    <div class="lightbox-bar">
      <div>
        <strong>ServoERP screenshot</strong>
        <span>Full-resolution product image</span>
      </div>
      <button class="lightbox-close" type="button" aria-label="Close image preview">Ã—</button>
    </div>
    <div class="lightbox-stage">
      <img alt="" />
    </div>
  `;
  document.body.appendChild(lightbox);

  const lightboxImage = lightbox.querySelector("img");
  const lightboxTitle = lightbox.querySelector("strong");
  const closeButton = lightbox.querySelector(".lightbox-close");
  let lastFocusedElement = null;

  const closeLightbox = () => {
    lightbox.hidden = true;
    lightboxImage.removeAttribute("src");
    document.body.classList.remove("has-lightbox");
    lastFocusedElement?.focus?.();
  };

  screenshotImages.forEach((image) => {
    image.loading = "eager";
    image.decoding = "sync";
    image.tabIndex = 0;
    image.setAttribute("role", "button");
    image.setAttribute("aria-label", `View full image: ${image.alt || "ServoERP screenshot"}`);
    image.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      lastFocusedElement = document.activeElement;
      lightboxImage.src = image.currentSrc || image.src;
      lightboxImage.alt = image.alt || "ServoERP screenshot";
      lightboxTitle.textContent = image.alt || "ServoERP screenshot";
      lightbox.hidden = false;
      document.body.classList.add("has-lightbox");
      closeButton.focus();
    });
    image.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        image.click();
      }
    });
  });

  document.querySelectorAll('a[href*="assets/screenshots"]').forEach((link) => {
    link.addEventListener("click", (event) => {
      event.preventDefault();
      if (event.target?.tagName === "IMG") {
        return;
      }
      link.querySelector("img")?.click();
    });
  });

  closeButton.addEventListener("click", closeLightbox);
  lightbox.addEventListener("click", (event) => {
    if (event.target === lightbox) {
      closeLightbox();
    }
  });
  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !lightbox.hidden) {
      closeLightbox();
    }
  });
}

const getLeadFormPayload = (form) => {
  const data = new FormData(form);
  return {
    name: data.get("name") || "",
    companyName: data.get("companyName") || data.get("company") || "",
    email: data.get("email") || "",
    phoneNumber: data.get("phoneNumber") || data.get("phone") || "",
    businessType: data.get("businessType") || data.get("industry") || "",
    message: data.get("message") || "",
    users: data.get("users") || "",
    plan: data.get("plan") || "",
    workflow: data.get("workflow") || "",
    website: data.get("website") || "",
  };
};

const setLeadFormStatus = (statusElement, message, type) => {
  if (!statusElement) {
    return;
  }
  statusElement.textContent = message;
  statusElement.dataset.state = type;
  statusElement.hidden = !message;
};

const buildDemoRequestEmail = (payload) => {
  const lines = [
    `Name: ${payload.name}`,
    `Company: ${payload.companyName}`,
    `Email: ${payload.email}`,
    `Phone: ${payload.phoneNumber}`,
    `Business type: ${payload.businessType}`,
    `Users: ${payload.users}`,
    `Plan: ${payload.plan}`,
    `Workflow: ${payload.workflow}`,
    "",
    "Message/Requirements:",
    payload.message,
  ].filter((line) => line !== undefined && line !== null);

  return `mailto:${DEMO_REQUEST_EMAIL}?subject=${encodeURIComponent("ServoERP demo request")}&body=${encodeURIComponent(lines.join("\n"))}`;
};

const openEmailFallback = (payload, statusElement) => {
  window.location.href = buildDemoRequestEmail(payload);
  setLeadFormStatus(statusElement, "Your email app is opening with the demo request. Please send it to complete the enquiry.", "success");
};

leadForm?.addEventListener("submit", async (event) => {
  event.preventDefault();

  if (!leadForm.reportValidity()) {
    return;
  }

  const submitButton = leadForm.querySelector('button[type="submit"]');
  const statusElement = leadForm.querySelector("[data-form-status]");
  const originalButtonText = submitButton?.textContent || "Send Demo Request";

  submitButton.disabled = true;
  submitButton.textContent = "Sending...";
  setLeadFormStatus(statusElement, "", "");
  const payload = getLeadFormPayload(leadForm);

  try {
    const response = await fetch(DEMO_REQUEST_ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    const result = await response.json().catch(() => ({}));

    if (!response.ok || !result.ok) {
      if (response.status === 404 || response.status >= 500) {
        openEmailFallback(payload, statusElement);
        return;
      }
      const firstError = result.errors ? Object.values(result.errors)[0] : null;
      throw new Error(firstError || result.error || "We could not submit your request right now.");
    }

    leadForm.reset();
    setLeadFormStatus(statusElement, result.message || DEMO_SUCCESS_MESSAGE, "success");
  } catch (error) {
    if (error instanceof TypeError) {
      openEmailFallback(payload, statusElement);
      return;
    }
    setLeadFormStatus(
      statusElement,
      error.message || "We could not submit your request right now. Please try again shortly.",
      "error"
    );
  } finally {
    submitButton.disabled = false;
    submitButton.textContent = originalButtonText;
  }
});

