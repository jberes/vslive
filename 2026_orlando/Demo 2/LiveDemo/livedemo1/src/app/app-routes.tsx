import Home from './home/home';
import Activity from './activity';
import Nutrition from './nutrition';
import Sleep from './sleep';
import MentalWellness from './mental-wellness';

export const routes = [
  { path: '/', element: <Home />, text: 'Home' },
  { path: '/activity', element: <Activity />, text: 'Activity' },
  { path: '/nutrition', element: <Nutrition />, text: 'Nutrition' },
  { path: '/sleep', element: <Sleep />, text: 'Sleep' },
  { path: '/mental-wellness', element: <MentalWellness />, text: 'Mental Wellness' },
  { path: '*', element: <Home />, text: 'Home' },
];
