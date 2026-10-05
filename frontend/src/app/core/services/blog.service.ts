import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { BlogPostDetail, BlogPostSummary, TravelMoment } from '../models/blog.model';

/** Public travel stories ("News & Blog") and the "Real Travel Moments" gallery. */
@Injectable({ providedIn: 'root' })
export class BlogService {
  constructor(private readonly http: HttpClient) {}

  getStories(limit = 30): Observable<BlogPostSummary[]> {
    return this.http
      .get<ApiResponse<BlogPostSummary[]>>(`${environment.apiUrl}/blog`, { params: { limit } })
      .pipe(map((r) => r.data ?? []));
  }

  getStory(slug: string): Observable<BlogPostDetail> {
    return this.http.get<ApiResponse<BlogPostDetail>>(`${environment.apiUrl}/blog/${slug}`).pipe(map((r) => r.data!));
  }

  getTravelMoments(): Observable<TravelMoment[]> {
    return this.http.get<ApiResponse<TravelMoment[]>>(`${environment.apiUrl}/travel-moments`).pipe(map((r) => r.data ?? []));
  }
}
