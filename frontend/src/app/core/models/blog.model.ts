export interface BlogPostSummary {
  blogPostId: number;
  title: string;
  slug: string;
  place: string | null;
  excerpt: string;
  /** The hero photo, or the story's first photo when it has no hero. */
  heroImageUrl: string | null;
  publishedAt: string | null;
  readMinutes: number;
}

/** One piece of the article, parsed by the API from the admin's plain text. */
export interface BlogBlock {
  type: 'h2' | 'p' | 'ul';
  text: string | null;
  items: string[] | null;
}

export interface BlogPhoto {
  blogPostPhotoId: number;
  imageUrl: string;
  caption: string | null;
}

export interface BlogPostDetail {
  blogPostId: number;
  title: string;
  slug: string;
  place: string | null;
  excerpt: string;
  heroImageUrl: string | null;
  publishedAt: string | null;
  updatedAt: string;
  readMinutes: number;
  blocks: BlogBlock[];
  tags: string[];
  photos: BlogPhoto[];
  moreStories: BlogPostSummary[];
}

export interface AdminBlogPostListItem {
  blogPostId: number;
  title: string;
  slug: string;
  place: string | null;
  isPublished: boolean;
  publishedAt: string | null;
  updatedAt: string;
  heroImageUrl: string | null;
  photoCount: number;
  tagCount: number;
}

export interface AdminBlogPostDetail {
  blogPostId: number;
  title: string;
  slug: string;
  place: string | null;
  excerpt: string;
  content: string;
  heroImageUrl: string | null;
  tags: string[];
  isPublished: boolean;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string;
  photos: BlogPhoto[];
}

export interface SaveBlogPostRequest {
  title: string;
  slug: string;
  place: string | null;
  excerpt: string;
  content: string;
  tags: string[];
  isPublished: boolean;
}

/** A photo in the home page's "Real Travel Moments" gallery. */
export interface TravelMoment {
  travelMomentId: number;
  imageUrl: string;
  caption: string | null;
  displayOrder: number;
}

/** Most photos the gallery holds (ITravelMomentService.MaxPhotos on the API). */
export const MAX_TRAVEL_MOMENTS = 10;
