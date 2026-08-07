import type { APIRoute } from 'astro';
import { apiFetch } from '../../../lib/api';

export const GET: APIRoute = async ({ params, cookies, redirect }) => {
  const documentId = params.id;
  if (!documentId) {
    return new Response('Not found', { status: 404 });
  }

  const response = await apiFetch(cookies, `/api/documents/${documentId}/download`);
  if (!response) {
    return redirect('/login');
  }

  if (!response.ok) {
    return new Response('Document not available.', { status: response.status });
  }

  const headers = new Headers();
  const contentType = response.headers.get('content-type');
  const contentDisposition = response.headers.get('content-disposition');
  if (contentType) headers.set('content-type', contentType);
  if (contentDisposition) headers.set('content-disposition', contentDisposition);

  return new Response(response.body, { status: 200, headers });
};
