const CHECKOUT_SRC = 'https://checkout.razorpay.com/v1/checkout.js';

let loading: Promise<void> | null = null;

/** True once Razorpay's checkout script has run and `window.Razorpay` exists. */
export function razorpayReady(): boolean {
  return typeof (window as { Razorpay?: unknown }).Razorpay === 'function';
}

/**
 * Loads Razorpay's checkout script on demand instead of on every page, so pages that never take a
 * payment don't wait for it. Safe to call repeatedly; a failed load can be retried.
 */
export function loadRazorpay(): Promise<void> {
  if (razorpayReady()) return Promise.resolve();
  if (loading) return loading;

  loading = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = CHECKOUT_SRC;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => {
      script.remove();
      loading = null;
      reject(new Error('Could not load the payment gateway.'));
    };
    document.head.appendChild(script);
  });
  return loading;
}
