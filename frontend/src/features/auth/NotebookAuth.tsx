import React, { useState, useEffect, useRef, useLayoutEffect } from 'react';
import './NotebookAuth.css';

export interface NotebookAuthProps {
  onLoginSuccess?: () => void;
  statusMode?: 'normal' | 'loading' | 'error' | 'success';
}

export const NotebookAuth: React.FC<NotebookAuthProps> = ({
  onLoginSuccess,
}) => {
  const [state, setState] = useState<'login' | 'register'>('login');
  const [busy, setBusy] = useState<boolean>(false);

  // Form inputs state
  const [loginId, setLoginId] = useState('demo@spendwise.vn');
  const [loginPass, setLoginPass] = useState('demo12345678');
  const [showLoginPass, setShowLoginPass] = useState(false);
  const [remember, setRemember] = useState(true);

  const [regName, setRegName] = useState('Nguyễn Văn An');
  const [regEmail, setRegEmail] = useState('an.nguyen@spendwise.vn');
  const [regPhone, setRegPhone] = useState('0912345678');
  const [regPass, setRegPass] = useState('matkhau12345');
  const [showRegPass, setShowRegPass] = useState(false);

  // Refs
  const bookRef = useRef<HTMLDivElement>(null);
  const leafRef = useRef<HTMLDivElement>(null);
  const shadeRef = useRef<HTMLDivElement>(null);
  const pgLoginRef = useRef<HTMLElement>(null);
  const pgRegRef = useRef<HTMLElement>(null);
  const sheetRootRef = useRef<HTMLDivElement | null>(null);

  const loginInputRef = useRef<HTMLInputElement>(null);
  const regInputRef = useRef<HTMLInputElement>(null);

  // Cuốn sổ có chiều cao cố định = 520px (như file mẫu)
  const TOP = 62;
  const N = 18;
  const DUR = 1150;
  const LEAD = 0.65;

  const ease = (t: number) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);

  // Đo một lần theo trang cao nhất + TOP (62px)
  const fit = () => {
    if (!pgLoginRef.current || !pgRegRef.current || !bookRef.current) return;
    const il = pgLoginRef.current.querySelector('.nb-inner') as HTMLElement | null;
    const ir = pgRegRef.current.querySelector('.nb-inner') as HTMLElement | null;
    if (il && ir) {
      il.style.minHeight = ir.style.minHeight = '';
      const contentH = Math.max(il.scrollHeight, ir.scrollHeight, il.offsetHeight, ir.offsetHeight);
      const H = Math.max(contentH + TOP, 595);
      bookRef.current.style.height = `${H}px`;
      il.style.minHeight = ir.style.minHeight = `${H - TOP}px`;
    }
  };

  useLayoutEffect(() => {
    fit();
    const onResize = () => fit();
    window.addEventListener('resize', onResize);

    // Dùng ResizeObserver theo dõi cả hai trang nội dung để tự động cập nhật
    let ro: ResizeObserver | null = null;
    if (typeof ResizeObserver !== 'undefined') {
      ro = new ResizeObserver(() => fit());
      const il = pgLoginRef.current?.querySelector('.nb-inner');
      const ir = pgRegRef.current?.querySelector('.nb-inner');
      if (il) ro.observe(il);
      if (ir) ro.observe(ir);
    }

    if (document.fonts) {
      document.fonts.ready.then(() => fit());
    }
    return () => {
      window.removeEventListener('resize', onResize);
      ro?.disconnect();
    };
  }, []);

  const stripsRef = useRef<
    {
      s: HTMLElement;
      f: HTMLElement;
      b: HTMLElement;
      shF: HTMLElement;
      shB: HTMLElement;
    }[]
  >([]);

  const makeHoles = (host: HTMLElement) => {
    host.innerHTML = '';
    const NR = 9;
    for (let i = 0; i < NR; i++) {
      const o = document.createElement('i');
      o.style.left = `${((i + 0.5) / NR) * 100}%`;
      host.appendChild(o);
    }
  };

  const buildSheet = () => {
    if (!bookRef.current || !pgLoginRef.current) return null;
    const W = bookRef.current.offsetWidth || 340;
    const hl = bookRef.current.offsetHeight || 520;
    const H = hl / N;

    const root = document.createElement('div');
    root.className = 'nb-flip';
    const strips: { s: HTMLElement; f: HTMLElement; b: HTMLElement; shF: HTMLElement; shB: HTMLElement }[] = [];
    let parent: HTMLElement = root;

    for (let i = 0; i < N; i++) {
      const s = document.createElement('div');
      s.className = 'nb-strip';
      s.style.top = `${i === 0 ? 0 : H}px`;
      s.style.height = `${H}px`;

      // Mặt trước: bản sao trang đăng nhập, cắt đúng dải i
      const f = document.createElement('div');
      f.className = 'nb-face front';
      f.style.height = `${H + 1}px`;

      const c = pgLoginRef.current.cloneNode(true) as HTMLElement;
      c.removeAttribute('id');
      c.setAttribute('inert', '');
      c.setAttribute('aria-hidden', 'true');
      c.querySelectorAll('[id]').forEach((el) => el.removeAttribute('id'));

      const src = pgLoginRef.current.querySelectorAll('input');
      const dst = c.querySelectorAll('input');
      dst.forEach((d, k) => {
        if (src[k]) {
          d.value = src[k].value;
          d.checked = src[k].checked;
        }
      });

      Object.assign(c.style, {
        position: 'absolute',
        inset: 'auto',
        left: '0',
        top: `${-i * H}px`,
        width: `${W}px`,
        height: `${hl}px`,
      });

      const shF = document.createElement('div');
      shF.className = 'nb-sh';
      f.append(c, shF);

      // Mặt sau: giấy trắng kẻ dòng, dải đầu có lỗ đục sát lò xo
      const b = document.createElement('div');
      b.className = 'nb-face back';
      b.style.height = `${H + 1}px`;
      b.style.backgroundImage = 'linear-gradient(to bottom, transparent 31px, var(--nb-line) 31px 32px)';
      b.style.backgroundSize = '100% 32px';
      b.style.backgroundPosition = `0 ${TOP - i * H}px`;

      if (i === 0) {
        const h = document.createElement('div');
        h.className = 'nb-holes';
        makeHoles(h);
        b.appendChild(h);
      }

      const shB = document.createElement('div');
      shB.className = 'nb-sh';
      b.appendChild(shB);

      s.append(f, b);
      parent.appendChild(s);
      parent = s;
      strips.push({ s, f, b, shF, shB });
    }

    root.addEventListener('click', () => go('login'));
    return { root, strips };
  };

  const pose = (e: number) => {
    let prev = 0;
    const hl = bookRef.current?.offsetHeight || 520;
    const H = hl / N;
    let y = 0; // Cumulative tip position

    // smoothstep((e - 0.55) / 0.4)
    const fadeT = Math.min(1, Math.max(0, (e - 0.55) / 0.4));
    const fadeOn = fadeT * fadeT * (3 - 2 * fadeT);

    stripsRef.current.forEach((o, i) => {
      const u = (i + 0.5) / N;
      const ep = Math.min(1, Math.max(0, e * (1 + LEAD) - LEAD * (1 - u)));
      const phi = ep * 180;
      o.s.style.transform = `rotateX(${phi - prev}deg)`;
      prev = phi;

      const rad = (phi * Math.PI) / 180;
      const sn = Math.sin(rad);
      o.shF.style.opacity = (0.42 * sn).toFixed(3);
      o.shB.style.opacity = (0.2 * sn).toFixed(3);

      // Height of strip tip: H * cos(phi)
      y += H * Math.cos(rad);

      // vis = clamp((y + 84 + 60) / 60, 0, 1)
      const vis = Math.min(1, Math.max(0, (y + 84 + 60) / 60));
      const op = 1 - fadeOn * (1 - vis);

      o.f.style.opacity = op.toFixed(3);
      o.b.style.opacity = op.toFixed(3);
      const isInteractable = op >= 0.05;
      o.f.style.pointerEvents = isInteractable ? 'auto' : 'none';
      o.b.style.pointerEvents = isInteractable ? 'auto' : 'none';
    });
    if (shadeRef.current) {
      shadeRef.current.style.opacity = Math.max(0, 1 - e * 1.15).toFixed(3);
    }
  };

  const mount = (e: number) => {
    const res = buildSheet();
    if (!res || !bookRef.current) return;
    sheetRootRef.current = res.root;
    stripsRef.current = res.strips;
    bookRef.current.appendChild(res.root);
    pose(e);
  };

  const unmount = () => {
    if (sheetRootRef.current) {
      sheetRootRef.current.remove();
      sheetRootRef.current = null;
    }
    stripsRef.current = [];
    if (shadeRef.current) shadeRef.current.style.opacity = '0';
  };

  const updateState = (next: 'login' | 'register') => {
    setState(next);
    const isLogin = next === 'login';
    if (pgLoginRef.current) {
      if (isLogin) pgLoginRef.current.removeAttribute('inert');
      else pgLoginRef.current.setAttribute('inert', '');
    }
    if (pgRegRef.current) {
      if (isLogin) pgRegRef.current.setAttribute('inert', '');
      else pgRegRef.current.removeAttribute('inert');
    }
    if (leafRef.current) {
      leafRef.current.style.visibility = isLogin ? 'visible' : 'hidden';
    }
    if (sheetRootRef.current) {
      sheetRootRef.current.classList.toggle('settled', !isLogin);
    }
    setTimeout(() => {
      if (isLogin) loginInputRef.current?.focus();
      else regInputRef.current?.focus();
    }, 30);
  };

  const go = (target: 'login' | 'register') => {
    if (busy || target === state) return;
    const forward = target === 'register';
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reduce) {
      if (forward) {
        unmount();
        mount(1);
      } else unmount();
      updateState(target);
      return;
    }

    setBusy(true);

    if (forward) {
      unmount();
      mount(0);
    }
    if (sheetRootRef.current) sheetRootRef.current.classList.remove('settled');
    if (leafRef.current) leafRef.current.style.visibility = 'hidden';
    pgLoginRef.current?.setAttribute('inert', '');
    pgRegRef.current?.setAttribute('inert', '');

    const from = forward ? 0 : 1;
    const to = forward ? 1 : 0;
    const t0 = performance.now();

    function frame(now: number) {
      const t = Math.min(1, (now - t0) / DUR);
      pose(from + (to - from) * ease(t));
      if (t < 1) {
        requestAnimationFrame(frame);
        return;
      }
      if (!forward) unmount();
      updateState(target);
      setBusy(false);
    }

    requestAnimationFrame(frame);
  };

  return (
    <div className="nb-auth-viewport">
      {/* SVG Symbols matching notebook-auth.html */}
      <svg width="0" height="0" style={{ position: 'absolute' }} aria-hidden="true">
        <symbol id="nb-i-user" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="8" r="4" />
          <path d="M4 21c0-4.4 3.6-7 8-7s8 2.6 8 7" />
        </symbol>
        <symbol id="nb-i-mail" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="5" width="18" height="14" rx="3" />
          <path d="m4 7 8 6 8-6" />
        </symbol>
        <symbol id="nb-i-phone" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2z" />
        </symbol>
        <symbol id="nb-i-lock" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <rect x="4" y="10" width="16" height="11" rx="3" />
          <path d="M8 10V7a4 4 0 0 1 8 0v3" />
        </symbol>
        <symbol id="nb-i-eye" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" />
          <circle cx="12" cy="12" r="3" />
        </symbol>
        <symbol id="nb-i-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M5 12h14M13 6l6 6-6 6" />
        </symbol>
        <symbol id="nb-i-spark" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 3l2 5 5 2-5 2-2 5-2-5-5-2 5-2z" />
        </symbol>
        <symbol id="nb-i-check" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="9" />
          <path d="m8 12 3 3 5-6" />
        </symbol>
      </svg>

      {/* CUỐN SỔ TAY */}
      <main className="nb-book" ref={bookRef}>
        {/* BÌA DA XANH ĐẬM */}
        <div className="nb-board">
          <i className="nb-corner tl" />
          <i className="nb-corner tr" />
          <i className="nb-corner bl" />
          <i className="nb-corner br" />
        </div>

        {/* CÁC LỚP GIẤY XẾP CHỒNG (STACK) */}
        <div className="nb-stack s1" />
        <div className="nb-stack s2" />
        <div className="nb-stack s3" />

        {/* TRANG ĐĂNG KÝ (NẰM DƯỚI) */}
        <section
          ref={pgRegRef}
          className="nb-page"
          aria-label="Trang đăng ký"
          {...(state !== 'register' ? { inert: '' } : {})}
        >
          <div className="nb-holes">
            {Array.from({ length: 9 }).map((_, i) => (
              <i key={i} style={{ left: `${((i + 0.5) / 9) * 100}%` }} />
            ))}
          </div>
          <div className="nb-inner">
            <div className="nb-head">
              <h2>
                <svg><use href="#nb-i-check" /></svg>
                Tạo trang sổ mới
              </h2>
            </div>
            <form
              autoComplete="on"
              onSubmit={(e) => {
                e.preventDefault();
                onLoginSuccess?.();
              }}
            >
              <div className="nb-field">
                <label htmlFor="nb-r-name">Họ và tên</label>
                <div className="nb-input">
                  <svg><use href="#nb-i-user" /></svg>
                  <input
                    id="nb-r-name"
                    ref={regInputRef}
                    value={regName}
                    onChange={(e) => setRegName(e.target.value)}
                    autoComplete="name"
                    required
                  />
                </div>
              </div>
              <div className="nb-field">
                <label htmlFor="nb-r-email">Email</label>
                <div className="nb-input">
                  <svg><use href="#nb-i-mail" /></svg>
                  <input
                    id="nb-r-email"
                    type="email"
                    value={regEmail}
                    onChange={(e) => setRegEmail(e.target.value)}
                    autoComplete="email"
                    required
                  />
                </div>
              </div>
              <div className="nb-field">
                <label htmlFor="nb-r-phone">Số điện thoại</label>
                <div className="nb-input">
                  <svg><use href="#nb-i-phone" /></svg>
                  <input
                    id="nb-r-phone"
                    type="tel"
                    value={regPhone}
                    onChange={(e) => setRegPhone(e.target.value)}
                    autoComplete="tel"
                    required
                  />
                </div>
              </div>
              <div className="nb-field">
                <label htmlFor="nb-r-pass">Mật khẩu</label>
                <div className="nb-input">
                  <svg><use href="#nb-i-lock" /></svg>
                  <input
                    id="nb-r-pass"
                    type={showRegPass ? 'text' : 'password'}
                    value={regPass}
                    onChange={(e) => setRegPass(e.target.value)}
                    autoComplete="new-password"
                    required
                  />
                  <button
                    type="button"
                    className="nb-eye"
                    aria-label="Hiện mật khẩu"
                    onClick={() => setShowRegPass(!showRegPass)}
                  >
                    <svg width="17" height="17"><use href="#nb-i-eye" /></svg>
                  </button>
                </div>
              </div>

              <button className="nb-btn" type="submit" disabled={busy}>
                Tạo sổ chi tiêu SpendWise <svg width="16" height="16"><use href="#nb-i-arrow" /></svg>
              </button>
            </form>

            <p className="nb-alt">Đã có tài khoản?</p>
            <button
              type="button"
              className="nb-btn-gold"
              disabled={busy}
              onClick={() => go('login')}
            >
              <svg width="15" height="15" style={{ transform: 'rotate(90deg)' }}>
                <use href="#nb-i-arrow" />
              </svg>
              ĐĂNG NHẬP NGAY
            </button>
          </div>
        </section>

        {/* LỚP BÓNG ĐỔ LÊN TRANG DƯỚI */}
        <div className="nb-under-shade" ref={shadeRef} />

        {/* TRANG ĐĂNG NHẬP (NẰM TRÊN, SẼ LẬT LÊN) */}
        <div className="nb-leaf" ref={leafRef}>
          <section
            ref={pgLoginRef}
            className="nb-page"
            aria-label="Trang đăng nhập"
            {...(state !== 'login' ? { inert: '' } : {})}
          >
            <div className="nb-holes">
              {Array.from({ length: 9 }).map((_, i) => (
                <i key={i} style={{ left: `${((i + 0.5) / 9) * 100}%` }} />
              ))}
            </div>
            <div className="nb-inner">
              <div className="nb-head">
                <h2>
                  <svg><use href="#nb-i-spark" /></svg>
                  Mở sổ chi tiêu
                </h2>
              </div>
              <form
                autoComplete="on"
                onSubmit={(e) => {
                  e.preventDefault();
                  onLoginSuccess?.();
                }}
              >
                <div className="nb-field">
                  <label htmlFor="nb-l-id">Email hoặc số điện thoại</label>
                  <div className="nb-input">
                    <svg><use href="#nb-i-mail" /></svg>
                    <input
                      id="nb-l-id"
                      ref={loginInputRef}
                      value={loginId}
                      onChange={(e) => setLoginId(e.target.value)}
                      autoComplete="username"
                      required
                    />
                  </div>
                </div>
                <div className="nb-field">
                  <label htmlFor="nb-l-pass">Mật khẩu</label>
                  <div className="nb-input">
                    <svg><use href="#nb-i-lock" /></svg>
                    <input
                      id="nb-l-pass"
                      type={showLoginPass ? 'text' : 'password'}
                      value={loginPass}
                      onChange={(e) => setLoginPass(e.target.value)}
                      autoComplete="current-password"
                      required
                    />
                    <button
                      type="button"
                      className="nb-eye"
                      aria-label="Hiện mật khẩu"
                      onClick={() => setShowLoginPass(!showLoginPass)}
                    >
                      <svg width="17" height="17"><use href="#nb-i-eye" /></svg>
                    </button>
                  </div>
                </div>

                <div className="nb-row">
                  <label className="nb-remember">
                    <input
                      type="checkbox"
                      checked={remember}
                      onChange={(e) => setRemember(e.target.checked)}
                    />
                    Ghi nhớ phiên đăng nhập
                  </label>
                  <button type="button" className="nb-link">
                    Quên mật khẩu?
                  </button>
                </div>

                <button className="nb-btn" type="submit" disabled={busy}>
                  Đăng nhập vào SpendWise <svg width="16" height="16"><use href="#nb-i-arrow" /></svg>
                </button>
              </form>

              <p className="nb-alt">Chưa có tài khoản?</p>
              <button
                type="button"
                className="nb-btn-gold"
                disabled={busy}
                onClick={() => go('register')}
              >
                ĐĂNG KÝ NGAY
                <svg width="15" height="15" style={{ transform: 'rotate(-90deg)' }}>
                  <use href="#nb-i-arrow" />
                </svg>
              </button>
            </div>
          </section>
        </div>

        {/* 9 VÒNG LÒ XO */}
        <div className="nb-rings">
          {Array.from({ length: 9 }).map((_, i) => (
            <div key={i} className="nb-ring" style={{ left: `${((i + 0.5) / 9) * 100}%` }} />
          ))}
        </div>
      </main>
    </div>
  );
};
