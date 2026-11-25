import { useState, type ReactNode } from 'react';
import { BrowserRouter, Navigate, NavLink, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import {
  AppBar, Box, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, ListSubheader, Toolbar, Tooltip, Typography,
  useMediaQuery, useTheme,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import LogoutIcon from '@mui/icons-material/Logout';
import DashboardIcon from '@mui/icons-material/SpaceDashboard';
import CategoryIcon from '@mui/icons-material/AccountTree';
import QuizIcon from '@mui/icons-material/Quiz';
import HelpIcon from '@mui/icons-material/LiveHelp';
import UploadIcon from '@mui/icons-material/UploadFile';
import FlagIcon from '@mui/icons-material/Flag';
import CodeIcon from '@mui/icons-material/Code';
import RouteIcon from '@mui/icons-material/Route';
import StyleIcon from '@mui/icons-material/Style';
import BusinessIcon from '@mui/icons-material/Business';
import PeopleIcon from '@mui/icons-material/People';
import PaymentsIcon from '@mui/icons-material/Payments';
import SmartToyIcon from '@mui/icons-material/SmartToy';
import CampaignIcon from '@mui/icons-material/Campaign';
import HistoryIcon from '@mui/icons-material/History';
import SettingsIcon from '@mui/icons-material/Settings';
import { useAuth, type Permissions } from './auth/AuthContext';
import { Loading } from './components/Common';
import LoginPage from './pages/Login';
import DashboardPage from './pages/Dashboard';
import TaxonomyPage from './pages/Taxonomy';
import { QuestionsPage, QuestionEditorPage } from './pages/Questions';
import ImportPage from './pages/Import';
import ReportsPage from './pages/Reports';
import QuizzesPage from './pages/Quizzes';
import { ChallengesPage, ChallengeEditorPage } from './pages/Challenges';
import { LearningPathsPage, LearningPathEditorPage } from './pages/LearningPaths';
import FlashcardsPage from './pages/Flashcards';
import { CompaniesPage, CompanyEditorPage } from './pages/Companies';
import UsersPage from './pages/Users';
import SubscriptionsPage from './pages/Subscriptions';
import AiPage from './pages/Ai';
import NotificationsPage from './pages/Notifications';
import AuditLogsPage from './pages/AuditLogs';
import SettingsPage from './pages/Settings';

type NavItem = { to: string; label: string; icon: ReactNode; needs: keyof Permissions };
const nav: { section: string; items: NavItem[] }[] = [
  { section: 'Overview', items: [{ to: '/', label: 'Dashboard', icon: <DashboardIcon />, needs: 'content' }] },
  {
    section: 'Content', items: [
      { to: '/taxonomy', label: 'Technologies & categories', icon: <CategoryIcon />, needs: 'content' },
      { to: '/questions', label: 'Questions', icon: <HelpIcon />, needs: 'content' },
      { to: '/import', label: 'Import / export', icon: <UploadIcon />, needs: 'content' },
      { to: '/reports', label: 'Question reports', icon: <FlagIcon />, needs: 'content' },
      { to: '/quizzes', label: 'Quizzes', icon: <QuizIcon />, needs: 'content' },
      { to: '/challenges', label: 'Coding challenges', icon: <CodeIcon />, needs: 'content' },
      { to: '/learning-paths', label: 'Learning paths', icon: <RouteIcon />, needs: 'content' },
      { to: '/flashcards', label: 'Flashcards', icon: <StyleIcon />, needs: 'content' },
      { to: '/companies', label: 'Companies', icon: <BusinessIcon />, needs: 'content' },
    ],
  },
  {
    section: 'Operations', items: [
      { to: '/users', label: 'Users', icon: <PeopleIcon />, needs: 'admin' },
      { to: '/subscriptions', label: 'Subscriptions & plans', icon: <PaymentsIcon />, needs: 'admin' },
      { to: '/ai', label: 'AI prompts & usage', icon: <SmartToyIcon />, needs: 'admin' },
      { to: '/notifications', label: 'Broadcast', icon: <CampaignIcon />, needs: 'admin' },
      { to: '/audit-logs', label: 'Audit logs', icon: <HistoryIcon />, needs: 'admin' },
      { to: '/settings', label: 'Settings', icon: <SettingsIcon />, needs: 'admin' },
    ],
  },
];

const drawerWidth = 260;

function Shell() {
  const { user, permissions, logout } = useAuth();
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up('md'));
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const drawer = (
    <Box>
      <Toolbar><Typography variant="h6" fontWeight={700}>DevInterview</Typography></Toolbar>
      <Divider />
      {nav.map((group) => {
        const items = group.items.filter((i) => permissions[i.needs]);
        if (!items.length) return null;
        return (
          <List key={group.section} dense subheader={<ListSubheader>{group.section}</ListSubheader>}>
            {items.map((i) => (
              <ListItemButton key={i.to} component={NavLink} to={i.to} end={i.to === '/'} onClick={() => setOpen(false)}
                selected={i.to === '/' ? location.pathname === '/' : location.pathname.startsWith(i.to)}>
                <ListItemIcon sx={{ minWidth: 36 }}>{i.icon}</ListItemIcon>
                <ListItemText primary={i.label} />
              </ListItemButton>
            ))}
          </List>
        );
      })}
    </Box>
  );
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider', zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          {!desktop && <IconButton edge="start" onClick={() => setOpen(true)} aria-label="Open navigation"><MenuIcon /></IconButton>}
          <Typography sx={{ flex: 1 }} variant="subtitle1" fontWeight={600}>Admin panel</Typography>
          <Typography variant="body2" sx={{ mr: 1, display: { xs: 'none', sm: 'block' } }}>{user?.email} · {user?.roles.join(', ')}</Typography>
          <Tooltip title="Sign out"><IconButton onClick={logout} aria-label="Sign out"><LogoutIcon /></IconButton></Tooltip>
        </Toolbar>
      </AppBar>
      <Drawer variant={desktop ? 'permanent' : 'temporary'} open={desktop || open} onClose={() => setOpen(false)}
        sx={{ width: drawerWidth, flexShrink: 0, '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' } }}>
        {drawer}
      </Drawer>
      <Box component="main" sx={{ flex: 1, p: { xs: 2, md: 3 }, mt: 8, minWidth: 0 }}><Outlet /></Box>
    </Box>
  );
}

function RequireAuth() {
  const { user, restoring } = useAuth();
  if (restoring) return <Loading />;
  return user ? <Shell /> : <Navigate to="/login" replace />;
}

function Guard({ needs, children }: { needs: keyof Permissions; children: ReactNode }) {
  const { permissions } = useAuth();
  return permissions[needs] ? <>{children}</> : <Navigate to="/" replace />;
}

export function AppRoutes() {
  const { user } = useAuth();
  return (
    <Routes>
      <Route path="/login" element={user ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route index element={<DashboardPage />} />
        <Route path="taxonomy" element={<TaxonomyPage />} />
        <Route path="questions" element={<QuestionsPage />} />
        <Route path="questions/new" element={<QuestionEditorPage />} />
        <Route path="questions/:id" element={<QuestionEditorPage />} />
        <Route path="import" element={<ImportPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="quizzes" element={<QuizzesPage />} />
        <Route path="challenges" element={<ChallengesPage />} />
        <Route path="challenges/new" element={<ChallengeEditorPage />} />
        <Route path="challenges/:id" element={<ChallengeEditorPage />} />
        <Route path="learning-paths" element={<LearningPathsPage />} />
        <Route path="learning-paths/new" element={<LearningPathEditorPage />} />
        <Route path="learning-paths/:id" element={<LearningPathEditorPage />} />
        <Route path="flashcards" element={<FlashcardsPage />} />
        <Route path="companies" element={<CompaniesPage />} />
        <Route path="companies/new" element={<CompanyEditorPage />} />
        <Route path="companies/:id" element={<CompanyEditorPage />} />
        <Route path="users" element={<Guard needs="admin"><UsersPage /></Guard>} />
        <Route path="subscriptions" element={<Guard needs="admin"><SubscriptionsPage /></Guard>} />
        <Route path="ai" element={<Guard needs="admin"><AiPage /></Guard>} />
        <Route path="notifications" element={<Guard needs="admin"><NotificationsPage /></Guard>} />
        <Route path="audit-logs" element={<Guard needs="admin"><AuditLogsPage /></Guard>} />
        <Route path="settings" element={<Guard needs="admin"><SettingsPage /></Guard>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}

export default function App() {
  return <BrowserRouter><AppRoutes /></BrowserRouter>;
}
