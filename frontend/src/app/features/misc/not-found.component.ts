import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="container" style="padding:80px 0;text-align:center">
      <h1 style="font-size:32px;margin-bottom:10px">Page not found</h1>
      <p class="mut" style="margin-bottom:20px">The page you're looking for doesn't exist or has moved.</p>
      <a class="btn" routerLink="/">Back to home</a>
    </div>
  `,
})
export class NotFoundComponent {}
