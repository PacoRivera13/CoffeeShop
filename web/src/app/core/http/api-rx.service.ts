import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { Api, ApiFnOptional, ApiFnRequired } from '../../api/api';
import { StrictHttpResponse } from '../../api/strict-http-response';

/**
 * Observable access to the generated API functions. Api.invoke returns promises, which cannot be cancelled;
 * switchMap and polling need observables so a stale request is dropped when the input changes.
 */
@Injectable({ providedIn: 'root' })
export class ApiRx {
  private readonly http = inject(HttpClient);
  private readonly api = inject(Api);

  response<P, R>(fn: ApiFnRequired<P, R> | ApiFnOptional<P, R>, params: P): Observable<StrictHttpResponse<R>> {
    return (fn as ApiFnRequired<P, R>)(this.http, this.api.rootUrl, params);
  }

  body<P, R>(fn: ApiFnRequired<P, R> | ApiFnOptional<P, R>, params: P): Observable<R> {
    return this.response(fn, params).pipe(map((response) => response.body));
  }
}
