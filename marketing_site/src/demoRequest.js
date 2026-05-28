const REQUIRED_FIELDS = [
  ["name", "Name"],
  ["companyName", "Company Name"],
  ["email", "Email"],
  ["phoneNumber", "Phone Number"],
  ["businessType", "Business Type"],
  ["message", "Message/Requirements"],
];

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_PATTERN = /^[+\d][\d\s().-]{6,}$/;
const SUCCESS_MESSAGE = "Thank you! Our team will contact you shortly.";
const BUSINESS_EMAIL = process.env.DEMO_REQUEST_TO_EMAIL || "support@servoerp.in";

const clean = (value) => String(value ?? "").trim();

export function validateDemoRequest(payload = {}) {
  if (clean(payload.website)) {
    return { ok: false, errors: { form: "Submission rejected." } };
  }

  const value = {
    name: clean(payload.name),
    companyName: clean(payload.companyName || payload.company),
    email: clean(payload.email).toLowerCase(),
    phoneNumber: clean(payload.phoneNumber || payload.phone),
    businessType: clean(payload.businessType || payload.industry),
    message: clean(payload.message),
    users: clean(payload.users),
    plan: clean(payload.plan),
    workflow: clean(payload.workflow),
    status: "Pending",
    source: "website",
  };

  const errors = {};
  for (const [key, label] of REQUIRED_FIELDS) {
    if (!value[key]) {
      errors[key] = `${label} is required.`;
    }
  }

  if (value.email && !EMAIL_PATTERN.test(value.email)) {
    errors.email = "Enter a valid business email.";
  }

  if (value.phoneNumber && !PHONE_PATTERN.test(value.phoneNumber)) {
    errors.phoneNumber = "Enter a valid phone number.";
  }

  if (value.message && value.message.length < 10) {
    errors.message = "Please add a little more detail.";
  }

  if (Object.keys(errors).length) {
    return { ok: false, errors };
  }

  return { ok: true, value };
}

function buildAdminEmail(request) {
  return {
    to: BUSINESS_EMAIL,
    subject: `ServoERP demo request - ${request.companyName}`,
    text: [
      "New ServoERP demo request",
      "",
      `Name: ${request.name}`,
      `Company: ${request.companyName}`,
      `Email: ${request.email}`,
      `Phone: ${request.phoneNumber}`,
      `Business Type: ${request.businessType}`,
      `Users/devices: ${request.users || "Not provided"}`,
      `Plan interest: ${request.plan || "Not provided"}`,
      `Primary workflow: ${request.workflow || "Not provided"}`,
      `Status: ${request.status}`,
      `Created At: ${request.createdAt}`,
      "",
      "Message/Requirements:",
      request.message,
    ].join("\n"),
  };
}

function buildCustomerEmail(request) {
  return {
    to: request.email,
    subject: "We received your ServoERP demo request",
    text: [
      `Hi ${request.name},`,
      "",
      "Thank you for requesting a ServoERP demo. Our team will review your requirements and contact you shortly.",
      "",
      "ServoERP Team",
    ].join("\n"),
  };
}

export function createDemoRequestService({ repository, sendMail }) {
  return {
    async submit(payload, metadata = {}) {
      const validation = validateDemoRequest(payload);
      if (!validation.ok) {
        return { ok: false, statusCode: 400, errors: validation.errors };
      }

      const saved = repository.save({
        ...validation.value,
        ipAddress: metadata.ipAddress || "",
        userAgent: metadata.userAgent || "",
      });

      await sendMail(buildAdminEmail(saved));
      await sendMail(buildCustomerEmail(saved));

      return {
        ok: true,
        statusCode: 201,
        message: SUCCESS_MESSAGE,
        request: {
          id: saved.id,
          status: saved.status,
          createdAt: saved.createdAt,
        },
      };
    },
  };
}

export { SUCCESS_MESSAGE };
