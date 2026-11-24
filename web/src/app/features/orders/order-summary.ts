import { OrderResponse } from '../../api/models/order-response';

/** Units across the order's lines. */
export function itemCount(order: OrderResponse): number {
  return order.lines.reduce((sum, line) => sum + Number(line.qty), 0);
}

/** Pay is only possible while the order is pending. */
export function canPay(status: string): boolean {
  return status === 'Pending';
}

/** The API cancels pending and paid orders; later states have left the roastery. */
export function canCancel(status: string): boolean {
  return status === 'Pending' || status === 'Paid';
}

/** Invoices exist once an order is paid, and stay downloadable as it moves on. */
export function hasInvoice(status: string): boolean {
  return status === 'Paid' || status === 'Preparing' || status === 'Shipped' || status === 'Delivered';
}
