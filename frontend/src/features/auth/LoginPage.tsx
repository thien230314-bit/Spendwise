import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../providers/AuthProvider';
import { AdminAuthGate, DEFAULT_ADMIN_ACCOUNT, DEFAULT_ADMIN_PASSWORD } from '../../screens/AdminAuthGate';

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const { isAuthenticated, currentUser } = useAuth();
  const [adminPassword, setAdminPassword] = useState(DEFAULT_ADMIN_PASSWORD);

  // If already authenticated as SystemAdmin, navigate straight to /admin
  React.useEffect(() => {
    if (isAuthenticated && currentUser?.role === 'SystemAdmin') {
      navigate('/admin', { replace: true });
    }
  }, [isAuthenticated, currentUser, navigate]);

  return (
    <div className="relative min-h-dvh w-full bg-[var(--bg)] text-[var(--ink)] flex flex-col justify-center items-center">
      <div aria-hidden="true" className="sw-mesh fixed inset-0 pointer-events-none" />
      <div className="relative z-10 w-full max-w-[480px] p-4">
        <AdminAuthGate
          adminAccount={DEFAULT_ADMIN_ACCOUNT}
          currentAdminPassword={adminPassword}
          onUpdateAdminPassword={setAdminPassword}
          onAuthenticated={() => {
            navigate('/admin', { replace: true });
          }}
          onExitToMobileApp={() => {
            window.location.href = 'http://localhost:8081';
          }}
        />
      </div>
    </div>
  );
};
