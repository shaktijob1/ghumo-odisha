import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, from, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { compressImage } from '../../shared/utils/compress-image';
import { ApiResponse } from '../models/api-response.model';
import { AdminBlogPostDetail, AdminBlogPostListItem, BlogPhoto, SaveBlogPostRequest, TravelMoment } from '../models/blog.model';

const blogBase = () => `${environment.apiUrl}/admin/blog`;
const momentsBase = () => `${environment.apiUrl}/admin/travel-moments`;

/** Admin: travel stories and the home page's "Real Travel Moments" photos. */
@Injectable({ providedIn: 'root' })
export class AdminBlogService {
  constructor(private readonly http: HttpClient) {}

  // ---------- Stories ----------

  getPosts(): Observable<AdminBlogPostListItem[]> {
    return this.http.get<ApiResponse<AdminBlogPostListItem[]>>(blogBase()).pipe(map((r) => r.data ?? []));
  }

  getPost(id: number): Observable<AdminBlogPostDetail> {
    return this.http.get<ApiResponse<AdminBlogPostDetail>>(`${blogBase()}/${id}`).pipe(map((r) => r.data!));
  }

  createPost(request: SaveBlogPostRequest): Observable<{ blogPostId: number }> {
    return this.http.post<ApiResponse<{ blogPostId: number }>>(blogBase(), request).pipe(map((r) => r.data!));
  }

  updatePost(id: number, request: SaveBlogPostRequest): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${blogBase()}/${id}`, request).pipe(map(() => undefined));
  }

  deletePost(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${blogBase()}/${id}`).pipe(map(() => undefined));
  }

  setHeroImage(id: number, file: File): Observable<void> {
    return this.upload<object>(`${blogBase()}/${id}/hero-image`, file, 1920).pipe(map(() => undefined));
  }

  addPhoto(id: number, file: File, caption?: string): Observable<BlogPhoto> {
    return this.upload<BlogPhoto>(`${blogBase()}/${id}/photos`, file, 1600, caption);
  }

  deletePhoto(id: number, photoId: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${blogBase()}/${id}/photos/${photoId}`).pipe(map(() => undefined));
  }

  // ---------- Real Travel Moments ----------

  addMoment(file: File, caption?: string): Observable<TravelMoment> {
    return this.upload<TravelMoment>(momentsBase(), file, 1600, caption);
  }

  updateMomentCaption(id: number, caption: string | null): Observable<void> {
    return this.http.put<ApiResponse<object>>(`${momentsBase()}/${id}/caption`, { caption }).pipe(map(() => undefined));
  }

  moveMoment(id: number, direction: -1 | 1): Observable<void> {
    return this.http.post<ApiResponse<object>>(`${momentsBase()}/${id}/move`, { direction }).pipe(map(() => undefined));
  }

  deleteMoment(id: number): Observable<void> {
    return this.http.delete<ApiResponse<object>>(`${momentsBase()}/${id}`).pipe(map(() => undefined));
  }

  /** Photos are shrunk in the browser first, so the site never serves multi-MB originals. */
  private upload<T>(url: string, file: File, maxSide: number, caption?: string): Observable<T> {
    return from(compressImage(file, maxSide)).pipe(
      switchMap((ready) => {
        const form = new FormData();
        form.append('file', ready);
        if (caption?.trim()) form.append('caption', caption.trim());
        return this.http.post<ApiResponse<T>>(url, form);
      }),
      map((r) => r.data!),
    );
  }
}
