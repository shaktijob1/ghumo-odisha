import { test, expect, type Page, type BrowserContext } from '@playwright/test';

/**
 * These tests mock the auth network calls instead of relying on real WhatsApp OTP delivery
 * (the WhatsApp OTP goes to a real phone we have no way to read in CI). Every other page (trips,
 * contact, etc.) still hits the real backend, so this only isolates the piece that can't be
 * automated without a live phone.
 */
const FAKE_SESSION = {
  token: 'e2e-fake-access-token',
  refreshToken: 'e2e-fake-refresh-token',
  customerId: 9001,
  name: 'Rahul Das',
  phoneNumber: '9876543210',
  email: 'rahul@example.com',
};

async function mockAuthApi(target: Page | BrowserContext): Promise<void> {
  await target.route('**/api/auth/customer/request-otp', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, message: '', data: { otpLength: 6 } }) }),
  );

  await target.route('**/api/auth/customer/verify-otp', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, message: '', data: FAKE_SESSION }) }),
  );

  await target.route('**/api/customer/profile', (route) => {
    if (route.request().method() !== 'GET') return route.continue();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        message: '',
        data: {
          customerId: FAKE_SESSION.customerId,
          name: FAKE_SESSION.name,
          phoneNumber: FAKE_SESSION.phoneNumber,
          email: FAKE_SESSION.email,
          isVerified: true,
          createdAt: '2026-01-01T00:00:00Z',
          lastLoginAt: '2026-01-01T00:00:00Z',
        },
      }),
    });
  });

  await target.route('**/api/auth/customer/refresh', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, message: '', data: FAKE_SESSION }) }),
  );

  await target.route('**/api/auth/customer/logout', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, message: '', data: {} }) }),
  );

  // Any other authenticated GET (bookings list, etc.) — return an empty-but-valid response so
  // unrelated pages render instead of erroring on a fake token the real backend doesn't know.
  await target.route('**/api/customer/bookings**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, message: '', data: { items: [], totalCount: 0, page: 1, pageSize: 50 } }),
    }),
  );
}

async function loginViaOtp(page: Page): Promise<void> {
  await page.goto('/login');
  await page.locator('.fld .inp').fill('9876543210');
  await page.getByRole('button', { name: 'Send OTP' }).click();

  await expect(page.locator('.pinbox input')).toHaveCount(6);
  const digits = '123456';
  for (let i = 0; i < 6; i++) {
    await page.locator('.pinbox input').nth(i).fill(digits[i]);
  }

  await page.waitForURL('**/my-bookings');
}

test.describe('Customer authentication & persistent session', () => {
  test('login via WhatsApp OTP shows the customer in the navbar, not Sign Out', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);

    const nav = page.locator('.cnav');
    await expect(nav).toContainText('Rahul');
    await expect(nav).not.toContainText('Sign out');
  });

  test('Sign Out lives on the profile page, not the navbar', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);

    await page.goto('/profile');
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
    await expect(page.locator('.cnav')).not.toContainText('Sign out');
  });

  test('session survives a page refresh', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);

    await page.reload();
    await expect(page.locator('.cnav')).toContainText('Rahul');
  });

  test('session survives navigating between pages', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);

    await page.goto('/trips');
    await expect(page.locator('.cnav')).toContainText('Rahul');
    await page.goto('/my-bookings');
    await expect(page.locator('.cnav')).toContainText('Rahul');
  });

  test('session survives closing and reopening the browser', async ({ browser }) => {
    const context1 = await browser.newContext();
    await mockAuthApi(context1);
    const page1 = await context1.newPage();
    await loginViaOtp(page1);
    const storageState = await context1.storageState();
    await context1.close();

    // A fresh context with the saved storage state is the standard way to simulate
    // "close the browser, reopen it later" — localStorage persists, nothing else does.
    const context2 = await browser.newContext({ storageState });
    await mockAuthApi(context2);
    const page2 = await context2.newPage();
    await page2.goto('/');
    await expect(page2.locator('.cnav')).toContainText('Rahul');
    await context2.close();
  });

  test('Sign Out clears the session and updates the navbar immediately', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);

    await page.goto('/profile');
    await page.getByRole('button', { name: 'Sign out' }).click();

    await expect(page.locator('.cnav')).toContainText('Sign in');
    await expect(page.locator('.cnav')).not.toContainText('Rahul');
    const stored = await page.evaluate(() => localStorage.getItem('go_customer_session'));
    expect(stored).toBeNull();
  });

  test('after Sign Out, refresh keeps the customer logged out', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);
    await page.goto('/profile');
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.locator('.cnav')).toContainText('Sign in');

    await page.reload();
    await expect(page.locator('.cnav')).toContainText('Sign in');
    await expect(page.locator('.cnav')).not.toContainText('Rahul');
  });

  test('customer can log in again after signing out', async ({ page }) => {
    await mockAuthApi(page);
    await loginViaOtp(page);
    await page.goto('/profile');
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.locator('.cnav')).toContainText('Sign in');

    await loginViaOtp(page);
    await expect(page.locator('.cnav')).toContainText('Rahul');
  });

  test('an invalid/expired session is cleared, not shown as authenticated forever', async ({ page }) => {
    // Seed a stored session whose refresh token the backend will reject, simulating expiry.
    await page.addInitScript((session) => {
      localStorage.setItem('go_customer_session', JSON.stringify(session));
    }, FAKE_SESSION);

    await page.route('**/api/customer/profile', (route) =>
      route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Unauthorized', errors: [] }) }),
    );
    await page.route('**/api/auth/customer/refresh', (route) =>
      route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Session expired.', errors: [] }) }),
    );

    await page.goto('/');
    await expect(page.locator('.cnav')).toContainText('Sign in');
    const stored = await page.evaluate(() => localStorage.getItem('go_customer_session'));
    expect(stored).toBeNull();
  });

  test('an already-authenticated customer booking a trip is not asked for OTP again', async ({ page }) => {
    await mockAuthApi(page);
    await page.route('**/api/bookings/request', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: '',
          data: {
            booking: { bookingId: 1, tripTitle: 'Koraput Escape', startDate: '2026-10-01', endDate: '2026-10-03', numberOfSeats: 1, totalAmount: 4999, bookingStatus: 0 },
            whatsAppMessage: 'Hi, I would like to book Koraput Escape',
          },
        }),
      }),
    );

    await page.addInitScript((session) => localStorage.setItem('go_customer_session', JSON.stringify(session)), FAKE_SESSION);

    await page.goto('/trips');
    await page.waitForSelector('.tcard');
    await page.locator('.tcard a.btn.sm').first().click();
    await page.waitForURL('**/trips/*');

    await expect(page.locator('.modal-backdrop')).toHaveCount(0);
    await page.locator('button:has-text("Book via WhatsApp")').first().click();

    await expect(page.locator('.successcard')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.modal-backdrop')).toHaveCount(0);
  });
});
