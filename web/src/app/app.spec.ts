import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page } from 'vitest/browser';
import { App } from './app';

describe('App', () => {
  it('renders the shell with the brand', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    await expect.element(page.getByTestId('brand')).toHaveTextContent('Roastery');
  });
});
