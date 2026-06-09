export default {
  async fetch(request, env) {
    const auth = request.headers.get("x-upload-token");
    if (!auth || auth !== env.UPLOAD_TOKEN) {
      return new Response("Unauthorized", { status: 401 });
    }

    const url = new URL(request.url);
    const key = decodeURIComponent(url.pathname.slice(1));
    if (!key) {
      return new Response("Missing object key", { status: 400 });
    }

    const action = url.searchParams.get("action");

    try {
      if (request.method === "GET" && action === "health") {
        return Response.json({ ok: true, key });
      }

      if (request.method === "POST" && action === "mpu-create") {
        const upload = await env.UPLOAD_BUCKET.createMultipartUpload(key);
        return Response.json({ key: upload.key, uploadId: upload.uploadId });
      }

      if (request.method === "PUT" && action === "mpu-uploadpart") {
        const uploadId = url.searchParams.get("uploadId");
        const partNumber = Number(url.searchParams.get("partNumber"));
        if (!uploadId || !partNumber) {
          return new Response("Missing uploadId or partNumber", { status: 400 });
        }

        const upload = env.UPLOAD_BUCKET.resumeMultipartUpload(key, uploadId);
        const part = await upload.uploadPart(partNumber, request.body);
        return Response.json(part);
      }

      if (request.method === "POST" && action === "mpu-complete") {
        const uploadId = url.searchParams.get("uploadId");
        if (!uploadId) {
          return new Response("Missing uploadId", { status: 400 });
        }

        const body = await request.json();
        const upload = env.UPLOAD_BUCKET.resumeMultipartUpload(key, uploadId);
        const object = await upload.complete(body.parts || []);
        return Response.json({ key: object.key, size: object.size, etag: object.etag });
      }

      if (request.method === "POST" && action === "mpu-abort") {
        const uploadId = url.searchParams.get("uploadId");
        if (!uploadId) {
          return new Response("Missing uploadId", { status: 400 });
        }

        const upload = env.UPLOAD_BUCKET.resumeMultipartUpload(key, uploadId);
        await upload.abort();
        return Response.json({ aborted: true });
      }

      return new Response("Unsupported route", { status: 404 });
    } catch (error) {
      const message = error && error.message ? error.message : String(error);
      return new Response(message, { status: 500 });
    }
  },
};
